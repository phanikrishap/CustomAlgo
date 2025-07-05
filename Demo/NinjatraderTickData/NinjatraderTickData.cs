using System;
using System.Globalization;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo.NinjatraderTickData
{
    /// <summary>
    /// Represents a tick data point from Ninjatrader format
    /// Format: yyyyMMdd HHmmss fffffff;price;price;price;volume
    /// </summary>
    public class NinjatraderTick
    {
        public DateTime Timestamp { get; set; }
        public double Price { get; set; }
        public uint Volume { get; set; }
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Parses a line from Ninjatrader tick data file
        /// </summary>
        public static NinjatraderTick ParseLine(string line, string symbol = "")
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be empty", nameof(line));

            var parts = line.Split(';');
            if (parts.Length < 5)
                throw new FormatException($"Invalid tick data format: {line}");

            // Parse timestamp: yyyyMMdd HHmmss fffffff
            var timestampParts = parts[0].Split(' ');
            if (timestampParts.Length != 3)
                throw new FormatException($"Invalid timestamp format: {parts[0]}");

            var datePart = timestampParts[0]; // yyyyMMdd
            var timePart = timestampParts[1]; // HHmmss
            var fractionPart = timestampParts[2]; // fffffff

            // Parse date: yyyyMMdd
            if (datePart.Length != 8)
                throw new FormatException($"Invalid date format: {datePart}");

            int year = int.Parse(datePart.Substring(0, 4));
            int month = int.Parse(datePart.Substring(4, 2));
            int day = int.Parse(datePart.Substring(6, 2));

            // Parse time: HHmmss
            if (timePart.Length != 6)
                throw new FormatException($"Invalid time format: {timePart}");

            int hour = int.Parse(timePart.Substring(0, 2));
            int minute = int.Parse(timePart.Substring(2, 2));
            int second = int.Parse(timePart.Substring(4, 2));

            // Parse fractions: fffffff (7 digits = 100 nanoseconds precision)
            int millisecond = 0;
            if (fractionPart.Length >= 3)
            {
                millisecond = int.Parse(fractionPart.Substring(0, 3));
            }

            // Create UTC timestamp and convert to IST
            var utcTimestamp = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
            var istTimestamp = TimeHelper.ToIST(utcTimestamp);

            // Parse price (taking the first price value)
            double price = double.Parse(parts[1], CultureInfo.InvariantCulture);

            // Parse volume
            uint volume = uint.Parse(parts[4]);

            return new NinjatraderTick
            {
                Timestamp = istTimestamp,
                Price = price,
                Volume = volume,
                Symbol = symbol
            };
        }

        public override string ToString()
        {
            return $"{Symbol} {Price:F2} Vol:{Volume} @ {TimeHelper.FormatIST(Timestamp, "HH:mm:ss.fff")}";
        }
    }
}