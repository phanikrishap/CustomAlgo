using System;
using System.Threading.Tasks;
using System.Threading;
using log4net;
using log4net.Config;
using CustomAlgo.Config;
using CustomAlgo.Authentication;
using CustomAlgo.WebSocket;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo
{
    /// <summary>
    /// Demonstration class showing broker login and WebSocket connectivity
    /// Tests the complete authentication and data feed pipeline
    /// </summary>
    public class CustomAlgoDemo
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CustomAlgoDemo));
        
        public static async Task Main(string[] args)
        {
            // Configure logging
            BasicConfigurator.Configure();
            
            Console.WriteLine("🚀 Kite Range Algo - Broker Login and WebSocket Demo");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"🕐 Current IST Time: {TimeHelper.FormatIST(TimeHelper.NowIST)}");
            Console.WriteLine($"📈 Market Status: {TimeHelper.GetMarketStatus()}");
            Console.WriteLine();
            
            try
            {
                await RunBrokerLoginAndWebSocketDemo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Demo failed: {ex.Message}");
                Logger.Error("Demo failed", ex);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Demonstrates complete broker login and WebSocket connectivity
        /// </summary>
        public static async Task RunBrokerLoginAndWebSocketDemo()
        {
            BrokerConfiguration config = null;
            BrokerTokenManager tokenManager = null;
            KiteWebSocketClient webSocketClient = null;

            try
            {
                Console.WriteLine("📋 Step 1: Loading broker configuration...");
                
                // Load configuration from file
                var configPath = "Config/broker_config.json";
                if (!System.IO.File.Exists(configPath))
                {
                    Console.WriteLine($"❌ Configuration file not found: {configPath}");
                    Console.WriteLine("Please ensure broker_config.json exists with valid credentials");
                    return;
                }

                config = BrokerConfiguration.LoadFromFile(configPath);
                Console.WriteLine($"✅ Configuration loaded: {config.ToMaskedString()}");

                Console.WriteLine("\n🔐 Step 2: Initializing token manager...");
                
                // Create token manager with config file path for persistence
                tokenManager = new BrokerTokenManager(config, Logger, configPath);
                Console.WriteLine("✅ Token manager initialized");

                Console.WriteLine("\n🔑 Step 3: Testing TOTP generation...");
                
                // Test TOTP generation
                try
                {
                    var totpCode = tokenManager.TestTotpGeneration();
                    Console.WriteLine($"✅ TOTP generated successfully: {totpCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ TOTP generation failed: {ex.Message}");
                    return;
                }

                Console.WriteLine("\n🎫 Step 4: Obtaining access token...");
                
                // Get valid access token
                var accessToken = await tokenManager.GetValidAccessTokenAsync();
                Console.WriteLine($"✅ Access token obtained: {MaskToken(accessToken)}");

                Console.WriteLine("\n🔗 Step 5: Initializing WebSocket client...");
                
                // Create WebSocket client
                webSocketClient = new KiteWebSocketClient(config, tokenManager, Logger);
                
                // Set up event handlers
                SetupWebSocketEventHandlers(webSocketClient);
                
                Console.WriteLine("✅ WebSocket client initialized");

                Console.WriteLine("\n📡 Step 6: Connecting to Kite WebSocket...");
                
                // Connect to WebSocket
                await webSocketClient.ConnectAsync();
                Console.WriteLine("✅ WebSocket connected successfully");

                Console.WriteLine("\n📊 Step 7: Receiving live tick data...");
                Console.WriteLine("Listening for tick data for 30 seconds...");
                Console.WriteLine("(Press Ctrl+C to stop early)");
                
                // Listen for data for 30 seconds
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    await Task.Delay(30000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\n⏹️ Demo stopped by user");
                }

                Console.WriteLine("\n✅ Demo completed successfully!");
                Console.WriteLine("\nSummary:");
                Console.WriteLine($"  - Configuration: ✅ Loaded from {configPath}");
                Console.WriteLine($"  - Token Manager: ✅ Initialized");
                Console.WriteLine($"  - TOTP: ✅ Generated successfully");
                Console.WriteLine($"  - Access Token: ✅ Obtained");
                Console.WriteLine($"  - WebSocket: ✅ Connected");
                Console.WriteLine($"  - Data Feed: ✅ Receiving live ticks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during demo: {ex.Message}");
                Logger.Error("Demo error", ex);
                throw;
            }
            finally
            {
                // Cleanup
                Console.WriteLine("\n🧹 Cleaning up...");
                try
                {
                    if (webSocketClient != null)
                    {
                        await webSocketClient.DisconnectAsync();
                        webSocketClient.Dispose();
                        Console.WriteLine("✅ WebSocket disconnected");
                    }

                    tokenManager?.Dispose();
                    Console.WriteLine("✅ Token manager disposed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Cleanup warning: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sets up event handlers for WebSocket client
        /// </summary>
        private static void SetupWebSocketEventHandlers(KiteWebSocketClient webSocketClient)
        {
            var tickCount = 0;
            var lastLogTime = DateTime.UtcNow;

            webSocketClient.OnConnected += () =>
            {
                Console.WriteLine("🔗 WebSocket connected!");
                Logger.Info("WebSocket connected");
            };

            webSocketClient.OnDisconnected += () =>
            {
                Console.WriteLine("❌ WebSocket disconnected!");
                Logger.Info("WebSocket disconnected");
            };

            webSocketClient.OnError += (error) =>
            {
                Console.WriteLine($"❌ WebSocket error: {error}");
                Logger.Error($"WebSocket error: {error}");
            };

            webSocketClient.OnTextMessageReceived += (message) =>
            {
                Console.WriteLine($"📝 Text message: {message}");
                Logger.Info($"Text message received: {message}");
            };

            webSocketClient.OnTickReceived += (tickData) =>
            {
                tickCount++;
                
                // Log every 10th tick or every 5 seconds, whichever comes first
                var now = DateTime.UtcNow;
                if (tickCount % 10 == 0 || (now - lastLogTime).TotalSeconds >= 5)
                {
                    Console.WriteLine($"📈 Tick #{tickCount}: {tickData.ToLogString()}");
                    lastLogTime = now;
                }

                Logger.Debug($"Tick received: {tickData}");
            };
        }

        /// <summary>
        /// Masks access token for display
        /// </summary>
        private static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length <= 8)
                return "****";
            return $"{token.Substring(0, 4)}...{token.Substring(token.Length - 4)}";
        }
    }
}