using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace CustomAlgo.Zerodha.Instruments
{
    /// <summary>
    /// SQLite database context for storing instrument data
    /// </summary>
    public class InstrumentsDbContext : DbContext
    {
        private readonly string _dbPath;

        public InstrumentsDbContext()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "instruments.db");
            
            // Ensure Data directory exists
            var dataDir = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir!);
        }

        public InstrumentsDbContext(string dbPath)
        {
            _dbPath = dbPath;
            
            // Ensure directory exists
            var dataDir = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir!);
        }

        public DbSet<InstrumentData> Instruments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure InstrumentData entity
            modelBuilder.Entity<InstrumentData>(entity =>
            {
                entity.HasKey(e => e.InstrumentToken);
                
                entity.HasIndex(e => e.TradingSymbol)
                      .HasDatabaseName("IX_Instruments_TradingSymbol");
                
                entity.HasIndex(e => e.Exchange)
                      .HasDatabaseName("IX_Instruments_Exchange");
                
                entity.HasIndex(e => new { e.Exchange, e.InstrumentType })
                      .HasDatabaseName("IX_Instruments_Exchange_Type");
                
                entity.HasIndex(e => e.LastUpdated)
                      .HasDatabaseName("IX_Instruments_LastUpdated");

                // Configure decimal precision
                entity.Property(e => e.LastPrice)
                      .HasPrecision(18, 2);
                
                entity.Property(e => e.Strike)
                      .HasPrecision(18, 2);
            });
        }

        /// <summary>
        /// Ensures database is created and migrations are applied
        /// </summary>
        public void EnsureCreated()
        {
            Database.EnsureCreated();
        }

        /// <summary>
        /// Gets database file path
        /// </summary>
        public string GetDatabasePath() => _dbPath;
    }
}