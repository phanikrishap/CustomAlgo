using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CustomAlgo.Zerodha.Instruments
{
    /// <summary>
    /// Represents instrument data from Kite Connect API
    /// </summary>
    [Table("Instruments")]
    public class InstrumentData
    {
        [Key]
        public long InstrumentToken { get; set; }

        public long ExchangeToken { get; set; }

        [Required]
        [MaxLength(50)]
        public string TradingSymbol { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public decimal LastPrice { get; set; }

        public DateTime? Expiry { get; set; }

        public decimal? Strike { get; set; }

        [Required]
        [MaxLength(10)]
        public string InstrumentType { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Exchange { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Segment { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LotSize { get; set; } = string.Empty;

        [MaxLength(10)]
        public string TickSize { get; set; } = string.Empty;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates InstrumentData from CSV row
        /// </summary>
        public static InstrumentData FromCsvRow(string csvRow)
        {
            var fields = csvRow.Split(',');
            if (fields.Length < 11)
                throw new ArgumentException($"Invalid CSV row format. Expected at least 11 fields, got {fields.Length}");

            return new InstrumentData
            {
                InstrumentToken = long.Parse(fields[0]),
                ExchangeToken = long.Parse(fields[1]),
                TradingSymbol = fields[2].Trim('"'),
                Name = fields[3].Trim('"'),
                LastPrice = decimal.TryParse(fields[4], out var price) ? price : 0,
                Expiry = DateTime.TryParse(fields[5], out var expiry) ? expiry : null,
                Strike = decimal.TryParse(fields[6], out var strike) ? strike : null,
                InstrumentType = fields[7].Trim('"'),
                Exchange = fields[8].Trim('"'),
                Segment = fields[9].Trim('"'),
                LotSize = fields.Length > 10 ? fields[10].Trim('"') : string.Empty,
                TickSize = fields.Length > 11 ? fields[11].Trim('"') : string.Empty,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Returns formatted string for display
        /// </summary>
        public override string ToString()
        {
            return $"{TradingSymbol} ({Exchange}) - {Name} [{InstrumentType}]";
        }

        /// <summary>
        /// Returns true if this is an equity instrument
        /// </summary>
        public bool IsEquity => InstrumentType.Equals("EQ", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns true if this is a derivative instrument
        /// </summary>
        public bool IsDerivative => InstrumentType.Equals("FUT", StringComparison.OrdinalIgnoreCase) ||
                                   InstrumentType.Equals("CE", StringComparison.OrdinalIgnoreCase) ||
                                   InstrumentType.Equals("PE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns true if this instrument has expired
        /// </summary>
        public bool IsExpired => Expiry.HasValue && Expiry.Value.Date < DateTime.Today;
    }
}