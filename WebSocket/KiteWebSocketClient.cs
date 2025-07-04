using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Net.WebSockets;
using System.IO;
using log4net;
using Newtonsoft.Json;

using CustomAlgo.Config;
using CustomAlgo.Authentication;
using CustomAlgo.Models;
using CustomAlgo.Utilities;

namespace CustomAlgo.WebSocket
{
    /// <summary>
    /// WebSocket client for Kite Connect WebSocket feed
    /// Handles connection, subscription, and real-time data reception
    /// </summary>
    public class KiteWebSocketClient : IDisposable
    {
        private readonly BrokerConfiguration _config;
        private readonly BrokerTokenManager _tokenManager;
        private readonly ILog _logger;
        
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _receiveTask;
        private bool _isConnected;
        private int _reconnectAttempts;

        // Events for data handling
        public event Action<TickData>? OnTickReceived;
        public event Action<string>? OnTextMessageReceived;
        public event Action<string>? OnError;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public KiteWebSocketClient(BrokerConfiguration config, BrokerTokenManager tokenManager, ILog logger = null!)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? LogManager.GetLogger(typeof(KiteWebSocketClient));
        }

        /// <summary>
        /// Connects to Kite WebSocket and subscribes to configured instruments
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                _logger.Info("[KiteWebSocket] Initiating connection to Kite WebSocket");
                
                // Get authentication details
                var authDetails = await _tokenManager.GetAuthenticationDetailsAsync();
                _logger.Info($"[KiteWebSocket] Authentication ready for API Key: {MaskApiKey(authDetails.ApiKey)}");

                // Create WebSocket URL with authentication
                var wsUrl = authDetails.GetWebSocketUrl();
                _logger.Info($"[KiteWebSocket] WebSocket URL: {MaskUrl(wsUrl)}");

                // Initialize WebSocket
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // Connect to WebSocket
                var uri = new Uri(wsUrl);
                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                
                _isConnected = true;
                _reconnectAttempts = 0;
                
                _logger.Info("[KiteWebSocket] Connected successfully");
                OnConnected?.Invoke();

                // Start receiving messages
                _receiveTask = Task.Run(ReceiveLoop, _cancellationTokenSource.Token);

                // Subscribe to configured instruments
                await SubscribeToInstrumentsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Connection failed", ex);
                OnError?.Invoke($"Connection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Subscribes to instruments specified in configuration
        /// </summary>
        private async Task SubscribeToInstrumentsAsync()
        {
            try
            {
                if (_config.Instruments == null || _config.Instruments.Count == 0)
                {
                    _logger.Warn("[KiteWebSocket] No instruments configured for subscription");
                    return;
                }

                _logger.Info($"[KiteWebSocket] Subscribing to {_config.Instruments.Count} instruments");

                // Group instruments by subscription mode
                var ltpTokens = new List<int>();
                var quoteTokens = new List<int>();
                var fullTokens = new List<int>();

                foreach (var instrument in _config.Instruments)
                {
                    switch (instrument.Mode.ToLower())
                    {
                        case "ltp":
                            ltpTokens.Add(instrument.Token);
                            break;
                        case "quote":
                            quoteTokens.Add(instrument.Token);
                            break;
                        case "full":
                            fullTokens.Add(instrument.Token);
                            break;
                    }
                }

                // Send subscription messages
                if (ltpTokens.Count > 0)
                {
                    await SendSubscriptionMessage("ltp", ltpTokens);
                    _logger.Info($"[KiteWebSocket] Subscribed to {ltpTokens.Count} instruments in LTP mode");
                }

                if (quoteTokens.Count > 0)
                {
                    await SendSubscriptionMessage("quote", quoteTokens);
                    _logger.Info($"[KiteWebSocket] Subscribed to {quoteTokens.Count} instruments in Quote mode");
                }

                if (fullTokens.Count > 0)
                {
                    await SendSubscriptionMessage("full", fullTokens);
                    _logger.Info($"[KiteWebSocket] Subscribed to {fullTokens.Count} instruments in Full mode");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Subscription failed", ex);
                OnError?.Invoke($"Subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends subscription message for specific mode and tokens
        /// </summary>
        private async Task SendSubscriptionMessage(string mode, List<int> tokens)
        {
            object subscriptionMessage;

            if (mode == "ltp")
            {
                subscriptionMessage = new
                {
                    a = "subscribe",
                    v = tokens
                };
            }
            else
            {
                subscriptionMessage = new
                {
                    a = "mode",
                    v = new Dictionary<string, List<int>> { { mode, tokens } }
                };
            }

            var json = JsonConvert.SerializeObject(subscriptionMessage);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket!.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource!.Token);

            _logger.Debug($"[KiteWebSocket] Sent subscription: {json}");
        }

        /// <summary>
        /// Main receive loop for WebSocket messages
        /// </summary>
        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            
            try
            {
                while (_isConnected && _webSocket?.State == WebSocketState.Open)
                {
                    var result = await _webSocket!.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource!.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.Info("[KiteWebSocket] Connection closed by server");
                        break;
                    }

                    await ProcessReceivedMessage(buffer, result);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("[KiteWebSocket] Receive loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Error in receive loop", ex);
                OnError?.Invoke($"Receive error: {ex.Message}");
                
                // Attempt reconnection if enabled
                if (_config.WebSocketSettings.AutoReconnect)
                {
                    _ = Task.Run(AttemptReconnection);
                }
            }
            finally
            {
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Processes received WebSocket message
        /// </summary>
        private Task ProcessReceivedMessage(byte[] buffer, WebSocketReceiveResult result)
        {
            try
            {
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Parse binary tick data
                    var tickData = ParseBinaryTickData(buffer, result.Count);
                    if (tickData != null)
                    {
                        OnTickReceived?.Invoke(tickData);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle text messages (errors, notifications)
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.Debug($"[KiteWebSocket] Text message received: {message}");
                    OnTextMessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Error processing message", ex);
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses binary tick data from Kite WebSocket
        /// </summary>
        private TickData? ParseBinaryTickData(byte[] buffer, int length)
        {
            try
            {
                if (length < 8) return null; // Minimum size for LTP data

                using var stream = new MemoryStream(buffer, 0, length);
                using var reader = new BinaryReader(stream);

                // Read instrument token (4 bytes, big-endian)
                var tokenBytes = reader.ReadBytes(4);
                Array.Reverse(tokenBytes);
                var instrumentToken = BitConverter.ToUInt32(tokenBytes, 0);

                // Read last traded price (4 bytes, big-endian)
                var priceBytes = reader.ReadBytes(4);
                Array.Reverse(priceBytes);
                var lastPrice = BitConverter.ToUInt32(priceBytes, 0) / 100.0; // Price is in paisa

                var tickData = new TickData
                {
                    InstrumentToken = instrumentToken,
                    LastPrice = lastPrice,
                    Timestamp = TimeHelper.NowIST
                };

                // Parse additional data based on message length
                if (length >= 44) // Quote data
                {
                    // Parse additional quote fields
                    ParseQuoteData(reader, tickData);
                }

                if (length >= 184) // Full data with market depth
                {
                    // Parse market depth data
                    ParseFullData(reader, tickData);
                }

                return tickData;
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Error parsing binary tick data", ex);
                return null;
            }
        }

        /// <summary>
        /// Parses quote data fields
        /// </summary>
        private void ParseQuoteData(BinaryReader reader, TickData tickData)
        {
            try
            {
                // Skip to OHLC data and volume (simplified parsing)
                reader.BaseStream.Seek(8, SeekOrigin.Begin); // Start after token and LTP
                
                // Read OHLC (4 bytes each, big-endian)
                tickData.Open = ReadBigEndianUInt32(reader) / 100.0;
                tickData.High = ReadBigEndianUInt32(reader) / 100.0;
                tickData.Low = ReadBigEndianUInt32(reader) / 100.0;
                tickData.Close = ReadBigEndianUInt32(reader) / 100.0;
                
                // Read volume
                tickData.Volume = ReadBigEndianUInt32(reader);
            }
            catch (Exception ex)
            {
                _logger.Warn("[KiteWebSocket] Error parsing quote data", ex);
            }
        }

        /// <summary>
        /// Parses full data with market depth
        /// </summary>
        private void ParseFullData(BinaryReader reader, TickData tickData)
        {
            try
            {
                // For now, just mark as full data
                // Market depth parsing can be added based on requirements
                tickData.IsFullData = true;
            }
            catch (Exception ex)
            {
                _logger.Warn("[KiteWebSocket] Error parsing full data", ex);
            }
        }

        /// <summary>
        /// Reads big-endian uint32 from binary reader
        /// </summary>
        private uint ReadBigEndianUInt32(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Attempts reconnection with exponential backoff
        /// </summary>
        private async Task AttemptReconnection()
        {
            if (_reconnectAttempts >= _config.WebSocketSettings.MaxReconnectAttempts)
            {
                _logger.Error("[KiteWebSocket] Maximum reconnection attempts reached");
                OnError?.Invoke("Maximum reconnection attempts reached");
                return;
            }

            _reconnectAttempts++;
            var delay = Math.Min(
                _config.WebSocketSettings.ReconnectInterval * _reconnectAttempts,
                30000); // Max 30 seconds

            _logger.Info($"[KiteWebSocket] Attempting reconnection {_reconnectAttempts}/{_config.WebSocketSettings.MaxReconnectAttempts} in {delay}ms");

            await Task.Delay(delay);

            try
            {
                await DisconnectAsync();
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"[KiteWebSocket] Reconnection attempt {_reconnectAttempts} failed", ex);
                
                // Try again if we haven't reached the limit
                if (_reconnectAttempts < _config.WebSocketSettings.MaxReconnectAttempts)
                {
                    _ = Task.Run(AttemptReconnection);
                }
            }
        }

        /// <summary>
        /// Disconnects from WebSocket
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _isConnected = false;
                _cancellationTokenSource?.Cancel();

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Disconnecting", 
                        CancellationToken.None);
                }

                _receiveTask?.Wait(5000); // Wait up to 5 seconds for receive task to complete
                
                _logger.Info("[KiteWebSocket] Disconnected successfully");
            }
            catch (Exception ex)
            {
                _logger.Error("[KiteWebSocket] Error during disconnection", ex);
            }
        }

        /// <summary>
        /// Gets connection status
        /// </summary>
        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        private string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
                return "****";
            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
        }

        private string MaskUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";
            
            // Mask the access_token parameter
            return System.Text.RegularExpressions.Regex.Replace(
                url, @"(access_token=)[^&\s""']+", "$1***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        public void Dispose()
        {
            try
            {
                DisconnectAsync().Wait(5000);
                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
                _logger?.Info("[KiteWebSocket] Disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error("[KiteWebSocket] Error during disposal", ex);
            }
        }
    }
}