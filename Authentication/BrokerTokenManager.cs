using System;
using System.Threading.Tasks;
using CustomAlgo.Config;
using CustomAlgo.Utilities;
using log4net;

namespace CustomAlgo.Authentication
{
    /// <summary>
    /// High-level token manager that integrates with broker configuration
    /// Provides a clean interface for token management across the application
    /// </summary>
    public class BrokerTokenManager : IDisposable
    {
        private readonly BrokerConfiguration _config;
        private readonly ZerodhaTokenService _tokenService;
        private readonly ILog _logger;
        private readonly string _configFilePath;

        public BrokerTokenManager(BrokerConfiguration config, ILog logger = null, string configFilePath = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? LogManager.GetLogger(typeof(BrokerTokenManager));
            _configFilePath = configFilePath;

            // Create token configuration from broker configuration
            var tokenConfig = new TokenConfiguration
            {
                ApiKey = _config.KiteCredentials.ApiKey,
                ApiSecret = _config.KiteCredentials.ApiSecret,
                UserId = _config.KiteCredentials.UserId,
                Password = _config.KiteCredentials.Password,
                TotpSecret = _config.KiteCredentials.TotpSecret,
                LocalPort = _config.KiteCredentials.LocalPort,
                TokenExpirationTime = TimeSpan.FromHours(_config.TokenSettings.ExpirationHours),
                LastAccessToken = _config.KiteCredentials.AccessToken,
                LastTokenGenerated = _config.KiteCredentials.AccessTokenTime
            };

            _tokenService = new ZerodhaTokenService(tokenConfig, _logger);
            _logger.Info("[BrokerTokenManager] Initialized with configuration");
        }

        /// <summary>
        /// Gets a valid access token for API calls
        /// </summary>
        public async Task<string> GetValidAccessTokenAsync()
        {
            try
            {
                _logger.Info("[BrokerTokenManager] Requesting valid access token");
                
                var token = await _tokenService.GetValidAccessTokenAsync();
                
                // Update the broker configuration with the token (using IST)
                _config.KiteCredentials.AccessToken = token;
                _config.KiteCredentials.AccessTokenTime = TimeHelper.NowIST;
                
                // Save to configuration file if path provided
                await SaveConfigurationAsync();
                
                _logger.Info("[BrokerTokenManager] Valid access token obtained and saved");
                return token;
            }
            catch (Exception ex)
            {
                _logger.Error("[BrokerTokenManager] Failed to get valid access token", ex);
                throw;
            }
        }

        /// <summary>
        /// Forces generation of a new access token
        /// </summary>
        public async Task<string> ForceGenerateNewTokenAsync()
        {
            try
            {
                _logger.Info("[BrokerTokenManager] Forcing new token generation");
                
                var token = await _tokenService.ForceGenerateNewTokenAsync();
                
                // Update the broker configuration with the new token (using IST)
                _config.KiteCredentials.AccessToken = token;
                _config.KiteCredentials.AccessTokenTime = TimeHelper.NowIST;
                
                // Save to configuration file if path provided
                await SaveConfigurationAsync();
                
                _logger.Info("[BrokerTokenManager] New token generated and saved successfully");
                return token;
            }
            catch (Exception ex)
            {
                _logger.Error("[BrokerTokenManager] Failed to force generate new token", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if the current token is expired
        /// </summary>
        public bool IsTokenExpired()
        {
            if (string.IsNullOrEmpty(_config.KiteCredentials.AccessToken) || 
                !_config.KiteCredentials.AccessTokenTime.HasValue)
                return true;

            var expirationTime = TimeSpan.FromHours(_config.TokenSettings.ExpirationHours);
            var elapsed = TimeHelper.NowIST - _config.KiteCredentials.AccessTokenTime.Value;
            
            return elapsed >= expirationTime;
        }

        /// <summary>
        /// Gets the current cached token (may be expired)
        /// </summary>
        public string GetCachedToken()
        {
            return _config.KiteCredentials.AccessToken;
        }

        /// <summary>
        /// Tests TOTP generation without performing full login
        /// </summary>
        public string TestTotpGeneration()
        {
            try
            {
                _logger.Info("[BrokerTokenManager] Testing TOTP generation");
                return _tokenService.TestTotpGeneration();
            }
            catch (Exception ex)
            {
                _logger.Error("[BrokerTokenManager] TOTP generation test failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets authentication details for WebSocket connection
        /// </summary>
        public async Task<AuthenticationDetails> GetAuthenticationDetailsAsync()
        {
            var token = await GetValidAccessTokenAsync();
            
            return new AuthenticationDetails
            {
                ApiKey = _config.KiteCredentials.ApiKey,
                AccessToken = token,
                TokenGeneratedAt = _config.KiteCredentials.AccessTokenTime ?? TimeHelper.NowIST
            };
        }

        /// <summary>
        /// Gets broker configuration (read-only)
        /// </summary>
        public BrokerConfiguration GetConfiguration()
        {
            return _config;
        }

        /// <summary>
        /// Gets configuration info with sensitive data masked
        /// </summary>
        public string GetConfigurationInfo()
        {
            return _config.ToMaskedString();
        }

        /// <summary>
        /// Saves the current configuration to file (if path was provided)
        /// </summary>
        private async Task SaveConfigurationAsync()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                _logger.Debug("[BrokerTokenManager] No config file path provided, skipping save");
                return;
            }

            try
            {
                _logger.Debug($"[BrokerTokenManager] Saving configuration to {_configFilePath}");
                _config.SaveToFile(_configFilePath);
                _logger.Debug("[BrokerTokenManager] Configuration saved successfully");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[BrokerTokenManager] Failed to save configuration: {ex.Message}");
                // Don't throw - configuration save failure shouldn't break token functionality
            }
        }

        /// <summary>
        /// Loads configuration from file and updates current config
        /// </summary>
        public async Task<bool> LoadConfigurationAsync()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                _logger.Debug("[BrokerTokenManager] No config file path provided, skipping load");
                return false;
            }

            try
            {
                _logger.Debug($"[BrokerTokenManager] Loading configuration from {_configFilePath}");
                var loadedConfig = BrokerConfiguration.LoadFromFile(_configFilePath);
                
                // Update current config with loaded values (especially access token)
                _config.KiteCredentials.AccessToken = loadedConfig.KiteCredentials.AccessToken;
                _config.KiteCredentials.AccessTokenTime = loadedConfig.KiteCredentials.AccessTokenTime;
                
                _logger.Debug("[BrokerTokenManager] Configuration loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[BrokerTokenManager] Failed to load configuration: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                _tokenService?.Dispose();
                _logger?.Info("[BrokerTokenManager] Disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error("[BrokerTokenManager] Error during disposal", ex);
            }
        }
    }

    /// <summary>
    /// Authentication details for WebSocket connection
    /// </summary>
    public class AuthenticationDetails
    {
        public string ApiKey { get; set; }
        public string AccessToken { get; set; }
        public DateTime TokenGeneratedAt { get; set; }
        
        /// <summary>
        /// Gets the WebSocket connection URL with authentication parameters
        /// </summary>
        public string GetWebSocketUrl()
        {
            return $"wss://ws.kite.trade?api_key={ApiKey}&access_token={AccessToken}";
        }
        
        /// <summary>
        /// Gets authorization header for HTTP requests
        /// </summary>
        public string GetAuthorizationHeader()
        {
            return $"token {ApiKey}:{AccessToken}";
        }
    }
}