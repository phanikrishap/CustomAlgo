using System;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using CustomAlgo.Config;
using CustomAlgo.Authentication;
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
                
                var configPath = "Config/broker_config.json";
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
    }
}