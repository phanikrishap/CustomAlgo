using System;
using System.Threading.Tasks;
using log4net;
namespace CustomAlgo.Authentication
{
    /// <summary>
    /// Main service for automated Zerodha token generation using the standalone ZerodhaTokenGenerator DLL
    /// Provides a clean interface for NseBrokerAPI to get valid access tokens
    /// Handles token caching and expiration logic
    /// </summary>
    public class ZerodhaTokenService : IDisposable
    {
        private readonly TokenConfiguration _configuration;
        private readonly ILog _logger;

        public ZerodhaTokenService(TokenConfiguration configuration, ILog logger = null!)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? LogManager.GetLogger(typeof(ZerodhaTokenService));
            
            // Validate configuration
            _configuration.Validate();
            
            _logger.Info($"[ZerodhaTokenService] Initialized with configuration: {_configuration.ToMaskedString()}");
        }

        /// <summary>
        /// Gets a valid access token, automatically generating a new one if expired
        /// This is the main method that NseBrokerAPI should call to get tokens
        /// </summary>
        /// <returns>Valid access token ready for Kite Connect API usage</returns>
        public async Task<string> GetValidAccessTokenAsync()
        {
            _logger.Info("[ZerodhaTokenService] Access token requested");

            try
            {
                // Check if current token is still valid
                if (!_configuration.IsTokenExpired() && !string.IsNullOrEmpty(_configuration.LastAccessToken))
                {
                    _logger.Info("[ZerodhaTokenService] Returning cached valid token");
                    return _configuration.LastAccessToken;
                }

                _logger.Info("[ZerodhaTokenService] Token expired or missing, generating new token");

                // Generate new token using automated capture
                var newToken = await GenerateNewTokenAsync();
                
                if (!string.IsNullOrEmpty(newToken))
                {
                    _configuration.UpdateToken(newToken);
                    _logger.Info("[ZerodhaTokenService] New access token generated and cached successfully");
                    return newToken;
                }

                throw new InvalidOperationException("Failed to generate access token through automated process");
            }
            catch (Exception ex)
            {
                _logger.Error("[ZerodhaTokenService] Failed to get valid access token", ex);
                throw;
            }
        }

        /// <summary>
        /// Forces generation of a new access token, bypassing cache
        /// </summary>
        /// <returns>Newly generated access token</returns>
        public async Task<string> ForceGenerateNewTokenAsync()
        {
            _logger.Info("[ZerodhaTokenService] Force token regeneration requested");
            
            var newToken = await GenerateNewTokenAsync();
            
            if (!string.IsNullOrEmpty(newToken))
            {
                _configuration.UpdateToken(newToken);
                _logger.Info("[ZerodhaTokenService] New access token force-generated successfully");
            }
            
            return newToken;
        }

        /// <summary>
        /// Checks if the current cached token is expired
        /// </summary>
        /// <returns>True if token is expired or missing</returns>
        public bool IsTokenExpired()
        {
            var expired = _configuration.IsTokenExpired();
            _logger.Debug($"[ZerodhaTokenService] Token expired status: {expired}");
            return expired;
        }

        /// <summary>
        /// Gets the current cached token without validation (may be expired)
        /// </summary>
        /// <returns>Current cached token or null</returns>
        public string GetCachedToken()
        {
            return _configuration.LastAccessToken;
        }

        /// <summary>
        /// Gets configuration information (masked for security)
        /// </summary>
        /// <returns>Masked configuration string</returns>
        public string GetConfigurationInfo()
        {
            return _configuration.ToMaskedString();
        }

        /// <summary>
        /// Tests TOTP generation without performing full login
        /// Useful for validating TOTP secret configuration
        /// </summary>
        /// <returns>Current TOTP code</returns>
        public string TestTotpGeneration()
        {
            _logger.Info("[ZerodhaTokenService] TOTP generation test requested");
            
            try
            {
                using (var tokenCapture = new AutomatedTokenCapture(
                    _configuration.ApiKey,
                    _configuration.ApiSecret,
                    _configuration.UserId,
                    _configuration.Password,
                    _configuration.TotpSecret,
                    _configuration.LocalPort))
                {
                    var totpCode = tokenCapture.GenerateTotp();
                    _logger.Info("[ZerodhaTokenService] TOTP generation test successful");
                    return totpCode;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[ZerodhaTokenService] TOTP generation test failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Updates the configuration with new credentials
        /// </summary>
        /// <param name="newConfiguration">New configuration to use</param>
        public void UpdateConfiguration(TokenConfiguration newConfiguration)
        {
            if (newConfiguration == null)
                throw new ArgumentNullException(nameof(newConfiguration));

            newConfiguration.Validate();

            // Preserve existing token if it's still valid and for the same API key
            if (!string.IsNullOrEmpty(_configuration.LastAccessToken) && 
                _configuration.ApiKey == newConfiguration.ApiKey &&
                !_configuration.IsTokenExpired())
            {
                newConfiguration.LastAccessToken = _configuration.LastAccessToken;
                newConfiguration.LastTokenGenerated = _configuration.LastTokenGenerated;
            }

            // Update configuration
            _configuration.ApiKey = newConfiguration.ApiKey;
            _configuration.ApiSecret = newConfiguration.ApiSecret;
            _configuration.UserId = newConfiguration.UserId;
            _configuration.Password = newConfiguration.Password;
            _configuration.TotpSecret = newConfiguration.TotpSecret;
            _configuration.LocalPort = newConfiguration.LocalPort;
            _configuration.TokenExpirationTime = newConfiguration.TokenExpirationTime;

            _logger.Info($"[ZerodhaTokenService] Configuration updated: {_configuration.ToMaskedString()}");
        }

        private async Task<string> GenerateNewTokenAsync()
        {
            _logger.Info("[ZerodhaTokenService] Starting automated token generation using standalone DLL");

            try
            {
                // Create new token generator instance using AutomatedTokenCapture
                using (var tokenCapture = new AutomatedTokenCapture(
                    _configuration.ApiKey,
                    _configuration.ApiSecret,
                    _configuration.UserId,
                    _configuration.Password,
                    _configuration.TotpSecret,
                    _configuration.LocalPort))
                {
                    _logger.Info("[ZerodhaTokenService] Starting automated token generation");

                    // Generate token using the automated capture
                    var accessToken = await tokenCapture.CaptureAccessTokenAsync();
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _logger.Info("[ZerodhaTokenService] Automated token generation completed successfully");
                        return accessToken;
                    }
                    else
                    {
                        _logger.Error("[ZerodhaTokenService] Token generation failed: No access token returned");
                        throw new InvalidOperationException("Failed to generate token: No access token returned");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[ZerodhaTokenService] Automated token generation failed", ex);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                // No need to dispose any token capture instances as they're now created and disposed locally
                _logger?.Info("[ZerodhaTokenService] Service disposed");
            }
            catch (Exception ex)
            {
                _logger?.Error("[ZerodhaTokenService] Error during disposal", ex);
            }
        }
    }
}