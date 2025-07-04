using System;
using System.ComponentModel.DataAnnotations;
using CustomAlgo.Utilities;

namespace CustomAlgo.Zerodha.Authentication
{
    /// <summary>
    /// Configuration model for Zerodha token automation
    /// </summary>
    public class TokenConfiguration
    {
        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string ApiSecret { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string TotpSecret { get; set; } = string.Empty;

        public int LocalPort { get; set; } = 8001;

        public TimeSpan TokenExpirationTime { get; set; } = TimeSpan.FromHours(6);

        public DateTime? LastTokenGenerated { get; set; }

        public string LastAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Checks if the current token is expired based on time and date
        /// </summary>
        public bool IsTokenExpired()
        {
            if (!LastTokenGenerated.HasValue || string.IsNullOrEmpty(LastAccessToken))
                return true;

            var currentIST = TimeHelper.NowIST;
            var tokenGeneratedIST = TimeHelper.ToIST(LastTokenGenerated.Value);
            
            // Check if token was generated on a different date (using IST)
            if (tokenGeneratedIST.Date != currentIST.Date)
                return true;

            // Check time-based expiration (using IST time)
            return currentIST - tokenGeneratedIST > TokenExpirationTime;
        }

        /// <summary>
        /// Updates the token information
        /// </summary>
        public void UpdateToken(string accessToken)
        {
            LastAccessToken = accessToken;
            LastTokenGenerated = DateTime.UtcNow;
        }

        /// <summary>
        /// Validates that all required configuration is present
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new ArgumentException("ApiKey is required");

            if (string.IsNullOrWhiteSpace(ApiSecret))
                throw new ArgumentException("ApiSecret is required");

            if (string.IsNullOrWhiteSpace(UserId))
                throw new ArgumentException("UserId is required");

            if (string.IsNullOrWhiteSpace(Password))
                throw new ArgumentException("Password is required");

            if (string.IsNullOrWhiteSpace(TotpSecret))
                throw new ArgumentException("TotpSecret is required");

            if (LocalPort <= 0 || LocalPort > 65535)
                throw new ArgumentException("LocalPort must be between 1 and 65535");
        }

        /// <summary>
        /// Creates a masked version for logging (hides sensitive data)
        /// </summary>
        public string ToMaskedString()
        {
            return $"ApiKey: {MaskValue(ApiKey)}, " +
                   $"UserId: {UserId}, " +
                   $"LocalPort: {LocalPort}, " +
                   $"HasToken: {!string.IsNullOrEmpty(LastAccessToken)}, " +
                   $"TokenExpired: {IsTokenExpired()}";
        }

        private static string MaskValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 8)
                return "****";

            return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
        }
    }
} 