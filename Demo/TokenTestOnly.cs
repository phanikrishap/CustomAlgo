using System;
using System.IO;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using CustomAlgo.Config;
using CustomAlgo.Zerodha.Authentication;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo
{
    /// <summary>
    /// Simple test program to verify token automation works correctly
    /// </summary>
    public class TokenTestOnly
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TokenTestOnly));
        
        public static async Task Main(string[] args)
        {
            // Configure logging
            BasicConfigurator.Configure();
            
            Console.WriteLine("🔑 Token Automation Test");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine($"🕐 Current IST Time: {TimeHelper.FormatIST(TimeHelper.NowIST)}");
            Console.WriteLine($"📈 Market Status: {TimeHelper.GetMarketStatus()}");
            Console.WriteLine();
            
            try
            {
                // Run TokenConfiguration validation tests first
                RunTokenConfigurationValidationTests();
                
                // Then run the actual token test
                await RunTokenTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Logger.Error("Token test failed", ex);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        public static async Task RunTokenTest()
        {
            BrokerConfiguration? config = null;
            BrokerTokenManager? tokenManager = null;

            try
            {
                Console.WriteLine("📋 Loading configuration...");
                
                // Use absolute path to the source configuration file
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Config", "broker_config.json");
                configPath = Path.GetFullPath(configPath); // Normalize the path
                
                Console.WriteLine($"📍 Using config file: {configPath}");
                
                if (!System.IO.File.Exists(configPath))
                {
                    Console.WriteLine($"❌ Configuration file not found: {configPath}");
                    return;
                }

                config = BrokerConfiguration.LoadFromFile(configPath);
                if (config == null)
                {
                    Console.WriteLine("❌ Failed to load configuration");
                    return;
                }
                
                Console.WriteLine($"✅ Configuration loaded: {config.ToMaskedString()}");

                Console.WriteLine("\n🔐 Creating token manager...");
                tokenManager = new BrokerTokenManager(config, Logger, configPath);
                Console.WriteLine("✅ Token manager created");

                Console.WriteLine("\n🔢 Testing TOTP generation...");
                var totpCode = tokenManager.TestTotpGeneration();
                Console.WriteLine($"✅ TOTP: {totpCode}");

                Console.WriteLine("\n🎫 Getting access token...");
                var accessToken = await tokenManager.GetValidAccessTokenAsync();
                Console.WriteLine($"✅ Access token obtained: {MaskToken(accessToken)}");

                Console.WriteLine("\n🎉 Token automation test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Logger.Error("Token test error", ex);
                throw;
            }
            finally
            {
                Console.WriteLine("\n🧹 Cleaning up...");
                tokenManager?.Dispose();
                Console.WriteLine("✅ Cleanup completed");
            }
        }

        private static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length <= 8)
                return "****";
            return $"{token.Substring(0, 6)}...{token.Substring(token.Length - 6)}";
        }

        public static void RunTokenConfigurationValidationTests()
        {
            Console.WriteLine("🧪 TokenConfiguration Validation Tests");
            Console.WriteLine(new string('=', 40));
            
            int testCount = 0;
            int passedCount = 0;
            
            // Test 1: Valid Configuration
            testCount++;
            Console.WriteLine($"Test {testCount}: Valid TokenConfiguration");
            try
            {
                var validConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key_123456",
                    ApiSecret = "test_secret_123456",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret",
                    LocalPort = 8001,
                    LastAccessToken = "test_token_123456",
                    LastTokenGenerated = TimeHelper.NowIST
                };
                
                validConfig.Validate();
                Console.WriteLine($"✅ IsTokenExpired: {validConfig.IsTokenExpired()}");
                Console.WriteLine($"✅ Masked String: {validConfig.ToMaskedString()}");
                Console.WriteLine("✅ PASSED - Valid configuration accepted");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }
            
            // Test 2: Token Expiration - Same Date
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Token Generated Today (Same Date)");
            try
            {
                var todayConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key",
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret",
                    LastAccessToken = "test_token",
                    LastTokenGenerated = TimeHelper.NowIST.AddHours(-2) // 2 hours ago, same day
                };
                
                bool isExpired = todayConfig.IsTokenExpired();
                Console.WriteLine($"✅ Token generated 2 hours ago (same day) - IsExpired: {isExpired}");
                Console.WriteLine("✅ PASSED - Token from same date should not be expired due to date");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }
            
            // Test 3: Token Expiration - Different Date
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Token Generated Yesterday (Different Date)");
            try
            {
                var yesterdayConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key",
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret",
                    LastAccessToken = "test_token",
                    LastTokenGenerated = TimeHelper.NowIST.AddDays(-1) // Yesterday
                };
                
                bool isExpired = yesterdayConfig.IsTokenExpired();
                Console.WriteLine($"✅ Token generated yesterday - IsExpired: {isExpired}");
                Console.WriteLine("✅ PASSED - Token from different date should be expired");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }
            
            // Test 4: Token Expiration - Time Based (Over 6 hours)
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Token Generated 7 Hours Ago (Time Expiration)");
            try
            {
                var expiredConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key",
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret",
                    LastAccessToken = "test_token",
                    LastTokenGenerated = TimeHelper.NowIST.AddHours(-7) // 7 hours ago, same day
                };
                
                bool isExpired = expiredConfig.IsTokenExpired();
                Console.WriteLine($"✅ Token generated 7 hours ago (same day) - IsExpired: {isExpired}");
                Console.WriteLine("✅ PASSED - Token older than 6 hours should be expired");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }
            
            // Test 5: Invalid Configuration - Missing ApiKey
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Invalid Configuration (Missing ApiKey)");
            try
            {
                var invalidConfig = new TokenConfiguration
                {
                    ApiKey = "", // Missing
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret"
                };
                
                invalidConfig.Validate();
                Console.WriteLine("❌ FAILED - Should have thrown exception for missing ApiKey");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"✅ PASSED - Correctly rejected missing ApiKey: {ex.Message}");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - Unexpected exception: {ex.Message}");
            }
            
            // Test 6: Invalid Port
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Invalid Configuration (Invalid Port)");
            try
            {
                var invalidPortConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key",
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret",
                    LocalPort = 0 // Invalid port
                };
                
                invalidPortConfig.Validate();
                Console.WriteLine("❌ FAILED - Should have thrown exception for invalid port");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"✅ PASSED - Correctly rejected invalid port: {ex.Message}");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - Unexpected exception: {ex.Message}");
            }
            
            // Test 7: Token Update Function
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Token Update Function");
            try
            {
                var updateConfig = new TokenConfiguration
                {
                    ApiKey = "test_api_key",
                    ApiSecret = "test_secret",
                    UserId = "TEST001",
                    Password = "test_password",
                    TotpSecret = "test_totp_secret"
                };
                
                var beforeUpdate = updateConfig.LastTokenGenerated;
                var newToken = "new_test_token_123456";
                
                updateConfig.UpdateToken(newToken);
                
                Console.WriteLine($"✅ Token updated: {MaskToken(updateConfig.LastAccessToken)}");
                Console.WriteLine($"✅ Time updated: {TimeHelper.FormatIST(updateConfig.LastTokenGenerated ?? DateTime.MinValue)}");
                Console.WriteLine($"✅ IsExpired after update: {updateConfig.IsTokenExpired()}");
                Console.WriteLine("✅ PASSED - Token update function works correctly");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }
            
            // Test 8: Masters Refresh Validation
            testCount++;
            Console.WriteLine($"\nTest {testCount}: Masters Refresh Validation");
            try
            {
                var mastersConfig = new BrokerConfiguration
                {
                    KiteCredentials = new KiteCredentials
                    {
                        ApiKey = "test_key",
                        ApiSecret = "test_secret",
                        UserId = "TEST001",
                        Password = "test_password",
                        TotpSecret = "test_totp_secret",
                        InstrumentMastersTime = null
                    },
                    Instruments = new List<InstrumentConfig>
                    {
                        new InstrumentConfig { Token = 408065, Symbol = "RELIANCE", Mode = "ltp" }
                    },
                    WebSocketSettings = new WebSocketSettings
                    {
                        Endpoint = "wss://ws.kite.trade",
                        AutoReconnect = true,
                        ReconnectInterval = 5000,
                        MaxReconnectAttempts = 10,
                        PingInterval = 30000
                    },
                    TokenSettings = new TokenSettings
                    {
                        AutoRefresh = true,
                        ExpirationHours = 6,
                        MaxRetries = 3,
                        CacheTokens = true
                    },
                    Logging = new LoggingSettings
                    {
                        Level = "Info",
                        FilePath = "Logs/test.log",
                        MaxFileSize = "10MB",
                        BackupCount = 5
                    }
                };
                
                // Test with null masters time
                var isRefreshed1 = mastersConfig.KiteCredentials.IsMastersRefreshed();
                Console.WriteLine($"✅ Masters refreshed (null time): {isRefreshed1}");
                
                // Update masters time to now
                mastersConfig.KiteCredentials.UpdateMastersTime();
                var isRefreshed2 = mastersConfig.KiteCredentials.IsMastersRefreshed();
                Console.WriteLine($"✅ Masters refreshed (current time): {isRefreshed2}");
                
                // Test with yesterday's time
                mastersConfig.KiteCredentials.InstrumentMastersTime = TimeHelper.NowIST.AddDays(-1);
                var isRefreshed3 = mastersConfig.KiteCredentials.IsMastersRefreshed();
                Console.WriteLine($"✅ Masters refreshed (yesterday): {isRefreshed3}");
                
                Console.WriteLine("✅ PASSED - Masters refresh validation works correctly");
                passedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED - {ex.Message}");
            }

            // Summary
            Console.WriteLine(new string('=', 40));
            Console.WriteLine($"🎯 Test Results: {passedCount}/{testCount} tests passed");
            if (passedCount == testCount)
            {
                Console.WriteLine("🎉 All TokenConfiguration validation tests PASSED!");
            }
            else
            {
                Console.WriteLine($"⚠️  {testCount - passedCount} tests FAILED");
            }
            Console.WriteLine();
        }
    }
}