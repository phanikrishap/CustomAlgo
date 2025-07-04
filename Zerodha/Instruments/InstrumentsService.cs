using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using log4net;
using Microsoft.EntityFrameworkCore;
using CustomAlgo.Utilities;
using CustomAlgo.Config;

namespace CustomAlgo.Zerodha.Instruments
{
    /// <summary>
    /// Service for fetching and managing Kite Connect instruments data
    /// </summary>
    public class InstrumentsService : IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(InstrumentsService));
        private readonly HttpClient _httpClient;
        private readonly InstrumentsDbContext _dbContext;
        private readonly string _apiKey;
        private readonly string _accessToken;
        private readonly BrokerConfiguration? _brokerConfig;
        private readonly string? _configFilePath;
        private bool _disposed = false;

        public InstrumentsService(string apiKey, string accessToken, string? dbPath = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Kite-Version", "3");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_apiKey}:{_accessToken}");
            
            _dbContext = string.IsNullOrEmpty(dbPath) ? new InstrumentsDbContext() : new InstrumentsDbContext(dbPath);
            _dbContext.EnsureCreated();

            Logger.Info($"[InstrumentsService] Initialized with database: {_dbContext.GetDatabasePath()}");
        }

        public InstrumentsService(string apiKey, string accessToken, BrokerConfiguration brokerConfig, string configFilePath, string? dbPath = null)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
            _brokerConfig = brokerConfig ?? throw new ArgumentNullException(nameof(brokerConfig));
            _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Kite-Version", "3");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_apiKey}:{_accessToken}");
            
            _dbContext = string.IsNullOrEmpty(dbPath) ? new InstrumentsDbContext() : new InstrumentsDbContext(dbPath);
            _dbContext.EnsureCreated();

            Logger.Info($"[InstrumentsService] Initialized with configuration integration, database: {_dbContext.GetDatabasePath()}");
        }

        /// <summary>
        /// Fetches and stores instruments for NSE exchange
        /// </summary>
        public async Task<int> FetchAndStoreNseInstrumentsAsync()
        {
            return await FetchAndStoreInstrumentsAsync("NSE");
        }

        /// <summary>
        /// Fetches and stores instruments for BSE exchange
        /// </summary>
        public async Task<int> FetchAndStoreBseInstrumentsAsync()
        {
            return await FetchAndStoreInstrumentsAsync("BSE");
        }

        /// <summary>
        /// Fetches and stores instruments for all supported exchanges
        /// </summary>
        public async Task<int> FetchAndStoreAllInstrumentsAsync()
        {
            var totalCount = 0;
            var exchanges = new[] { "NSE", "BSE", "MCX", "NFO" };

            foreach (var exchange in exchanges)
            {
                try
                {
                    var count = await FetchAndStoreInstrumentsAsync(exchange);
                    totalCount += count;
                    Logger.Info($"[InstrumentsService] Stored {count} instruments for {exchange}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[InstrumentsService] Failed to fetch instruments for {exchange}", ex);
                }
            }

            return totalCount;
        }

        /// <summary>
        /// Fetches and stores instruments for specified exchange
        /// </summary>
        public async Task<int> FetchAndStoreInstrumentsAsync(string exchange)
        {
            try
            {
                Logger.Info($"[InstrumentsService] Fetching instruments for {exchange}...");
                
                var url = $"https://api.kite.trade/instruments/{exchange}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Read and decompress CSV data
                var csvData = await DecompressResponseAsync(response);
                var instruments = ParseCsvData(csvData, exchange);

                // Clear existing data for this exchange and save new data
                var storedCount = await StoreInstrumentsAsync(instruments, exchange);
                
                Logger.Info($"[InstrumentsService] Successfully stored {storedCount} instruments for {exchange}");
                return storedCount;
            }
            catch (Exception ex)
            {
                Logger.Error($"[InstrumentsService] Failed to fetch instruments for {exchange}", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets instruments by exchange from database
        /// </summary>
        public async Task<List<InstrumentData>> GetInstrumentsByExchangeAsync(string exchange)
        {
            return await _dbContext.Instruments
                .Where(i => i.Exchange == exchange)
                .OrderBy(i => i.TradingSymbol)
                .ToListAsync();
        }

        /// <summary>
        /// Searches instruments by trading symbol
        /// </summary>
        public async Task<List<InstrumentData>> SearchInstrumentsAsync(string searchTerm, string? exchange = null)
        {
            var query = _dbContext.Instruments.AsQueryable();

            if (!string.IsNullOrEmpty(exchange))
                query = query.Where(i => i.Exchange == exchange);

            return await query
                .Where(i => i.TradingSymbol.Contains(searchTerm) || i.Name.Contains(searchTerm))
                .OrderBy(i => i.TradingSymbol)
                .Take(100) // Limit results
                .ToListAsync();
        }

        /// <summary>
        /// Gets instrument by token
        /// </summary>
        public async Task<InstrumentData?> GetInstrumentByTokenAsync(long instrumentToken)
        {
            return await _dbContext.Instruments
                .FirstOrDefaultAsync(i => i.InstrumentToken == instrumentToken);
        }

        /// <summary>
        /// Gets database statistics
        /// </summary>
        public async Task<Dictionary<string, int>> GetDatabaseStatsAsync()
        {
            var stats = new Dictionary<string, int>();
            
            var exchanges = await _dbContext.Instruments
                .GroupBy(i => i.Exchange)
                .Select(g => new { Exchange = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var exchange in exchanges)
            {
                stats[exchange.Exchange] = exchange.Count;
            }

            stats["Total"] = await _dbContext.Instruments.CountAsync();
            
            return stats;
        }

        /// <summary>
        /// Checks if data needs refresh based on configuration or database age
        /// </summary>
        public async Task<bool> NeedsRefreshAsync()
        {
            // If we have broker configuration, use the IsMastersRefreshed method
            if (_brokerConfig != null)
            {
                return !_brokerConfig.KiteCredentials.IsMastersRefreshed();
            }
            
            // Fallback to database-based checking
            var latestUpdate = await _dbContext.Instruments
                .MaxAsync(i => (DateTime?)i.LastUpdated);

            if (!latestUpdate.HasValue)
                return true;

            // Refresh if data is older than 1 day or if it's a new trading day
            var istNow = TimeHelper.NowIST;
            var lastUpdateIst = TimeHelper.ToIST(latestUpdate.Value);
            
            return lastUpdateIst.Date < istNow.Date;
        }

        /// <summary>
        /// Fetches and stores instruments with automatic configuration update
        /// </summary>
        public async Task<int> FetchAndStoreInstrumentsWithConfigAsync(string exchange)
        {
            var count = await FetchAndStoreInstrumentsAsync(exchange);
            
            // Update configuration if available
            if (_brokerConfig != null && !string.IsNullOrEmpty(_configFilePath))
            {
                try
                {
                    _brokerConfig.KiteCredentials.UpdateMastersTime();
                    await Task.Run(() => _brokerConfig.SaveToFile(_configFilePath));
                    Logger.Info($"[InstrumentsService] Updated masters time in configuration for {exchange}");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[InstrumentsService] Failed to update configuration: {ex.Message}");
                }
            }
            
            return count;
        }

        /// <summary>
        /// Fetches and stores all instruments with automatic configuration update
        /// </summary>
        public async Task<int> FetchAndStoreAllInstrumentsWithConfigAsync()
        {
            var totalCount = await FetchAndStoreAllInstrumentsAsync();
            
            // Update configuration if available
            if (_brokerConfig != null && !string.IsNullOrEmpty(_configFilePath))
            {
                try
                {
                    _brokerConfig.KiteCredentials.UpdateMastersTime();
                    await Task.Run(() => _brokerConfig.SaveToFile(_configFilePath));
                    Logger.Info($"[InstrumentsService] Updated masters time in configuration after fetching all exchanges");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[InstrumentsService] Failed to update configuration: {ex.Message}");
                }
            }
            
            return totalCount;
        }

        private async Task<string> DecompressResponseAsync(HttpResponseMessage response)
        {
            var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
            
            using var stream = await response.Content.ReadAsStreamAsync();
            
            if (contentEncoding?.Equals("gzip", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                return await reader.ReadToEndAsync();
            }
            else
            {
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
        }

        private List<InstrumentData> ParseCsvData(string csvData, string exchange)
        {
            var instruments = new List<InstrumentData>();
            var lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    
                    var instrument = InstrumentData.FromCsvRow(line);
                    instruments.Add(instrument);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[InstrumentsService] Failed to parse CSV line {i}: {ex.Message}");
                }
            }

            return instruments;
        }

        private async Task<int> StoreInstrumentsAsync(List<InstrumentData> instruments, string exchange)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            
            try
            {
                // Remove existing data for this exchange
                var existingInstruments = _dbContext.Instruments.Where(i => i.Exchange == exchange);
                _dbContext.Instruments.RemoveRange(existingInstruments);
                
                // Add new instruments in batches
                const int batchSize = 1000;
                var storedCount = 0;
                
                for (int i = 0; i < instruments.Count; i += batchSize)
                {
                    var batch = instruments.Skip(i).Take(batchSize);
                    await _dbContext.Instruments.AddRangeAsync(batch);
                    await _dbContext.SaveChangesAsync();
                    storedCount += batch.Count();
                }
                
                await transaction.CommitAsync();
                return storedCount;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _dbContext?.Dispose();
                _disposed = true;
                Logger.Info("[InstrumentsService] Disposed successfully");
            }
        }
    }
}