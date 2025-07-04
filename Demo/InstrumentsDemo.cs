using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using CustomAlgo.Config;
using CustomAlgo.Zerodha.Authentication;
using CustomAlgo.Zerodha.Instruments;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo
{
    /// <summary>
    /// Demo application for testing Instruments service functionality
    /// </summary>
    public class InstrumentsDemo
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(InstrumentsDemo));
        
        public static async Task Main(string[] args)
        {
            // Configure logging
            BasicConfigurator.Configure();
            
            Console.WriteLine("📊 Kite Instruments Demo");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine($"🕐 Current IST Time: {TimeHelper.FormatIST(TimeHelper.NowIST)}");
            Console.WriteLine($"📈 Market Status: {TimeHelper.GetMarketStatus()}");
            Console.WriteLine();
            
            try
            {
                await RunInstrumentsDemo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Demo failed: {ex.Message}");
                Logger.Error("Instruments demo failed", ex);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        public static async Task RunInstrumentsDemo()
        {
            BrokerConfiguration? config = null;
            BrokerTokenManager? tokenManager = null;
            InstrumentsService? instrumentsService = null;

            try
            {
                Console.WriteLine("📋 Loading configuration...");
                
                // Use absolute path to the source configuration file
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Config", "broker_config.json");
                configPath = Path.GetFullPath(configPath);
                
                Console.WriteLine($"📍 Using config file: {configPath}");
                
                if (!File.Exists(configPath))
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

                Console.WriteLine("\n🔐 Getting access token...");
                tokenManager = new BrokerTokenManager(config, Logger, configPath);
                var accessToken = await tokenManager.GetValidAccessTokenAsync();
                Console.WriteLine($"✅ Access token obtained: {MaskToken(accessToken)}");

                Console.WriteLine("\n📊 Initializing Instruments service...");
                instrumentsService = new InstrumentsService(
                    config.KiteCredentials.ApiKey, 
                    accessToken,
                    config,
                    configPath,
                    Path.Combine(Directory.GetCurrentDirectory(), "Data", "instruments.db")
                );
                Console.WriteLine("✅ Instruments service initialized with configuration integration");

                // Check if refresh is needed
                Console.WriteLine("\n🔍 Checking if data refresh is needed...");
                Console.WriteLine($"📅 Current masters time: {config.KiteCredentials.InstrumentMastersTime?.ToString() ?? "null"}");
                Console.WriteLine($"📅 Is masters refreshed: {config.KiteCredentials.IsMastersRefreshed()}");
                var needsRefresh = await instrumentsService.NeedsRefreshAsync();
                Console.WriteLine($"✅ Needs refresh: {needsRefresh}");

                if (needsRefresh)
                {
                    Console.WriteLine("\n📥 Fetching NSE instruments...");
                    var nseCount = await instrumentsService.FetchAndStoreInstrumentsWithConfigAsync("NSE");
                    Console.WriteLine($"✅ Stored {nseCount} NSE instruments");

                    Console.WriteLine("\n📥 Fetching BSE instruments...");
                    var bseCount = await instrumentsService.FetchAndStoreInstrumentsWithConfigAsync("BSE");
                    Console.WriteLine($"✅ Stored {bseCount} BSE instruments");
                    
                    Console.WriteLine("✅ Configuration updated with latest masters time");
                }
                else
                {
                    Console.WriteLine("ℹ️ Using cached instrument data (masters already refreshed today)");
                }

                // Show database statistics
                Console.WriteLine("\n📈 Database Statistics:");
                var stats = await instrumentsService.GetDatabaseStatsAsync();
                foreach (var stat in stats)
                {
                    Console.WriteLine($"  {stat.Key}: {stat.Value:N0} instruments");
                }

                // Search examples
                Console.WriteLine("\n🔍 Search Examples:");
                
                Console.WriteLine("\n  Searching for 'RELIANCE':");
                var relianceResults = await instrumentsService.SearchInstrumentsAsync("RELIANCE", "NSE");
                foreach (var instrument in relianceResults.Take(5))
                {
                    Console.WriteLine($"    {instrument.TradingSymbol} - {instrument.Name} [{instrument.InstrumentType}] (Token: {instrument.InstrumentToken})");
                }

                Console.WriteLine("\n  Searching for 'INFY':");
                var infyResults = await instrumentsService.SearchInstrumentsAsync("INFY");
                foreach (var instrument in infyResults.Take(5))
                {
                    Console.WriteLine($"    {instrument.TradingSymbol} - {instrument.Name} [{instrument.InstrumentType}] (Token: {instrument.InstrumentToken})");
                }

                // Get specific instrument by token
                if (relianceResults.Any())
                {
                    var firstReliance = relianceResults.First();
                    Console.WriteLine($"\n🎯 Getting instrument by token {firstReliance.InstrumentToken}:");
                    var instrumentByToken = await instrumentsService.GetInstrumentByTokenAsync(firstReliance.InstrumentToken);
                    if (instrumentByToken != null)
                    {
                        Console.WriteLine($"    Found: {instrumentByToken}");
                        Console.WriteLine($"    Last Price: ₹{instrumentByToken.LastPrice}");
                        Console.WriteLine($"    Is Equity: {instrumentByToken.IsEquity}");
                        Console.WriteLine($"    Is Derivative: {instrumentByToken.IsDerivative}");
                    }
                }

                Console.WriteLine("\n🎉 Instruments demo completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Logger.Error("Instruments demo error", ex);
                throw;
            }
            finally
            {
                Console.WriteLine("\n🧹 Cleaning up...");
                instrumentsService?.Dispose();
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