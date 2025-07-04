using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;

namespace CustomAlgo.Config
{
    /// <summary>
    /// Configuration model for broker credentials and settings
    /// </summary>
    public class BrokerConfiguration
    {
        [JsonProperty("kite_credentials")]
        public required KiteCredentials KiteCredentials { get; set; }

        [JsonProperty("instruments")]
        public List<InstrumentConfig> Instruments { get; set; } = new List<InstrumentConfig>();

        [JsonProperty("websocket_settings")]
        public required WebSocketSettings WebSocketSettings { get; set; } = new WebSocketSettings();

        [JsonProperty("token_settings")]
        public required TokenSettings TokenSettings { get; set; } = new TokenSettings();

        [JsonProperty("logging")]
        public required LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Loads configuration from JSON file
        /// </summary>
        public static BrokerConfiguration? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<BrokerConfiguration>(json);
            
            config?.Validate();
            return config;
        }

        /// <summary>
        /// Saves configuration to JSON file
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Validates configuration completeness
        /// </summary>
        public void Validate()
        {
            if (KiteCredentials == null)
                throw new ArgumentException("KiteCredentials is required");

            KiteCredentials.Validate();

            if (Instruments == null || Instruments.Count == 0)
                throw new ArgumentException("At least one instrument must be configured");

            foreach (var instrument in Instruments)
            {
                instrument.Validate();
            }

            WebSocketSettings.Validate();
            TokenSettings.Validate();
            
            // Logging settings are optional and have defaults
        }

        /// <summary>
        /// Creates a masked version for logging (hides sensitive data)
        /// </summary>
        public string ToMaskedString()
        {
            return $"API Key: {MaskValue(KiteCredentials?.ApiKey)}, " +
                   $"User ID: {KiteCredentials?.UserId}, " +
                   $"Instruments: {Instruments?.Count ?? 0}, " +
                   $"WebSocket: {WebSocketSettings.Endpoint}";
        }

        private static string MaskValue(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 8)
                return "****";

            return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
        }
    }

    /// <summary>
    /// Kite Connect API credentials
    /// </summary>
    public class KiteCredentials
    {
        [JsonProperty("api_key")]
        [Required]
        public required string ApiKey { get; set; }

        [JsonProperty("api_secret")]
        [Required]
        public required string ApiSecret { get; set; }

        [JsonProperty("user_id")]
        [Required]
        public required string UserId { get; set; }

        [JsonProperty("password")]
        [Required]
        public required string Password { get; set; }

        [JsonProperty("totp_secret")]
        [Required]
        public required string TotpSecret { get; set; }

        [JsonProperty("local_port")]
        public int LocalPort { get; set; } = 8001;

        [JsonProperty("redirect_url")]
        public string RedirectUrl { get; set; } = "http://127.0.0.1:8001/callback";

        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("access_token_time")]
        public DateTime? AccessTokenTime { get; set; }

        /// <summary>
        /// Token generation timestamp (for backward compatibility)
        /// </summary>
        [JsonIgnore]
        public DateTime? TokenGeneratedAt 
        { 
            get => AccessTokenTime;
            set => AccessTokenTime = value;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new ArgumentException("API Key is required");

            if (string.IsNullOrWhiteSpace(ApiSecret))
                throw new ArgumentException("API Secret is required");

            if (string.IsNullOrWhiteSpace(UserId))
                throw new ArgumentException("User ID is required");

            if (string.IsNullOrWhiteSpace(Password))
                throw new ArgumentException("Password is required");

            if (string.IsNullOrWhiteSpace(TotpSecret))
                throw new ArgumentException("TOTP Secret is required");

            if (LocalPort <= 0 || LocalPort > 65535)
                throw new ArgumentException("Local port must be between 1 and 65535");

            if (string.IsNullOrWhiteSpace(RedirectUrl))
                throw new ArgumentException("Redirect URL is required");
        }
    }

    /// <summary>
    /// Instrument configuration for WebSocket subscription
    /// </summary>
    public class InstrumentConfig
    {
        [JsonProperty("token")]
        public int Token { get; set; }

        [JsonProperty("symbol")]
        public required string Symbol { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; } = "ltp";

        public void Validate()
        {
            if (Token <= 0)
                throw new ArgumentException("Invalid instrument token");

            if (string.IsNullOrWhiteSpace(Symbol))
                throw new ArgumentException("Symbol is required");

            if (!IsValidMode(Mode))
                throw new ArgumentException($"Invalid subscription mode: {Mode}. Valid modes: ltp, quote, full");
        }

        private static bool IsValidMode(string? mode)
        {
            return mode?.ToLower() switch
            {
                "ltp" => true,
                "quote" => true,
                "full" => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// WebSocket connection settings
    /// </summary>
    public class WebSocketSettings
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; } = "wss://ws.kite.trade";

        [JsonProperty("auto_reconnect")]
        public bool AutoReconnect { get; set; } = true;

        [JsonProperty("reconnect_interval")]
        public int ReconnectInterval { get; set; } = 5000;

        [JsonProperty("max_reconnect_attempts")]
        public int MaxReconnectAttempts { get; set; } = 10;

        [JsonProperty("ping_interval")]
        public int PingInterval { get; set; } = 30000;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
                throw new ArgumentException("WebSocket endpoint is required");

            if (ReconnectInterval < 1000)
                throw new ArgumentException("Reconnect interval must be at least 1000ms");

            if (MaxReconnectAttempts < 1)
                throw new ArgumentException("Max reconnect attempts must be at least 1");
        }
    }

    /// <summary>
    /// Token management settings
    /// </summary>
    public class TokenSettings
    {
        [JsonProperty("auto_refresh")]
        public bool AutoRefresh { get; set; } = true;

        [JsonProperty("expiration_hours")]
        public int ExpirationHours { get; set; } = 6;

        [JsonProperty("max_retries")]
        public int MaxRetries { get; set; } = 3;

        [JsonProperty("cache_tokens")]
        public bool CacheTokens { get; set; } = true;

        public void Validate()
        {
            if (ExpirationHours < 1 || ExpirationHours > 24)
                throw new ArgumentException("Expiration hours must be between 1 and 24");

            if (MaxRetries < 1)
                throw new ArgumentException("Max retries must be at least 1");
        }
    }

    /// <summary>
    /// Logging configuration
    /// </summary>
    public class LoggingSettings
    {
        [JsonProperty("level")]
        public string Level { get; set; } = "Info";

        [JsonProperty("file_path")]
        public string FilePath { get; set; } = "logs/kite_algo.log";

        [JsonProperty("max_file_size")]
        public string MaxFileSize { get; set; } = "10MB";

        [JsonProperty("backup_count")]
        public int BackupCount { get; set; } = 5;
    }
}