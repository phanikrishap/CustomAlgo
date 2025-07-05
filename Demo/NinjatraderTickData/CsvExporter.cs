using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomAlgo.Models;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo.NinjatraderTickData
{
    /// <summary>
    /// Exports bar data to CSV files
    /// </summary>
    public static class CsvExporter
    {
        /// <summary>
        /// Exports minute bars to CSV
        /// </summary>
        public static void ExportMinuteBars(List<OHLCData> bars, string filePath)
        {
            var sortedBars = bars.OrderBy(b => b.Timestamp).ToList();
            
            using var writer = new StreamWriter(filePath);
            
            // Write header
            writer.WriteLine("DateTime,Symbol,Open,High,Low,Close,Volume,Range,Body,IsBullish");
            
            // Write data
            foreach (var bar in sortedBars)
            {
                writer.WriteLine($"{TimeHelper.FormatIST(bar.Timestamp, "yyyy-MM-dd HH:mm:ss")}," +
                               $"{bar.Symbol}," +
                               $"{bar.Open:F2}," +
                               $"{bar.High:F2}," +
                               $"{bar.Low:F2}," +
                               $"{bar.Close:F2}," +
                               $"{bar.Volume}," +
                               $"{bar.Range:F2}," +
                               $"{bar.Body:F2}," +
                               $"{bar.IsBullish}");
            }
            
            Console.WriteLine($"Exported {sortedBars.Count} minute bars to {filePath}");
        }

        /// <summary>
        /// Exports Range ATR bars to CSV
        /// </summary>
        public static void ExportRangeATRBars(List<RangeATRBar> bars, string filePath)
        {
            var sortedBars = bars.OrderBy(b => b.Timestamp).ToList();
            
            using var writer = new StreamWriter(filePath);
            
            // Write header
            writer.WriteLine("DateTime,Symbol,Open,High,Low,Close,Volume,Range,Body,IsBullish," +
                           "ATRValue,RangeThreshold,TickCount,BarDuration");
            
            // Write data
            foreach (var bar in sortedBars)
            {
                var barDuration = bar.LastUpdateTime - bar.BarStartTime;
                
                writer.WriteLine($"{TimeHelper.FormatIST(bar.Timestamp, "yyyy-MM-dd HH:mm:ss")}," +
                               $"{bar.Symbol}," +
                               $"{bar.Open:F2}," +
                               $"{bar.High:F2}," +
                               $"{bar.Low:F2}," +
                               $"{bar.Close:F2}," +
                               $"{bar.Volume}," +
                               $"{bar.Range:F2}," +
                               $"{bar.Body:F2}," +
                               $"{bar.IsBullish}," +
                               $"{bar.ATRValue:F2}," +
                               $"{bar.RangeThreshold:F2}," +
                               $"{bar.TickCount}," +
                               $"{barDuration.TotalSeconds:F1}");
            }
            
            Console.WriteLine($"Exported {sortedBars.Count} Range ATR bars to {filePath}");
        }

        /// <summary>
        /// Exports tick data to CSV for verification
        /// </summary>
        public static void ExportTickData(List<NinjatraderTick> ticks, string filePath)
        {
            var sortedTicks = ticks.OrderBy(t => t.Timestamp).ToList();
            
            using var writer = new StreamWriter(filePath);
            
            // Write header
            writer.WriteLine("DateTime,Symbol,Price,Volume");
            
            // Write data
            foreach (var tick in sortedTicks)
            {
                writer.WriteLine($"{TimeHelper.FormatIST(tick.Timestamp, "yyyy-MM-dd HH:mm:ss.fff")}," +
                               $"{tick.Symbol}," +
                               $"{tick.Price:F2}," +
                               $"{tick.Volume}");
            }
            
            Console.WriteLine($"Exported {sortedTicks.Count} ticks to {filePath}");
        }

        /// <summary>
        /// Creates a summary report of the processed data
        /// </summary>
        public static void CreateSummaryReport(List<NinjatraderTick> ticks, 
                                             List<OHLCData> minuteBars, 
                                             List<RangeATRBar> rangeATRBars, 
                                             string filePath)
        {
            using var writer = new StreamWriter(filePath);
            
            writer.WriteLine("=== NINJATRADER TICK DATA PROCESSING SUMMARY ===");
            writer.WriteLine($"Generated on: {TimeHelper.FormatIST(DateTime.Now)}");
            writer.WriteLine();
            
            // Tick data summary
            writer.WriteLine("TICK DATA SUMMARY:");
            writer.WriteLine($"Total ticks processed: {ticks.Count:N0}");
            if (ticks.Count > 0)
            {
                var firstTick = ticks.OrderBy(t => t.Timestamp).First();
                var lastTick = ticks.OrderBy(t => t.Timestamp).Last();
                writer.WriteLine($"Time range: {TimeHelper.FormatIST(firstTick.Timestamp)} to {TimeHelper.FormatIST(lastTick.Timestamp)}");
                writer.WriteLine($"Duration: {(lastTick.Timestamp - firstTick.Timestamp).TotalHours:F1} hours");
                writer.WriteLine($"Price range: {ticks.Min(t => t.Price):F2} to {ticks.Max(t => t.Price):F2}");
                writer.WriteLine($"Total volume: {ticks.Sum(t => (long)t.Volume):N0}");
            }
            writer.WriteLine();
            
            // Minute bars summary
            writer.WriteLine("MINUTE BARS SUMMARY:");
            writer.WriteLine($"Total minute bars: {minuteBars.Count:N0}");
            if (minuteBars.Count > 0)
            {
                var avgVolume = minuteBars.Average(b => b.Volume);
                var avgRange = minuteBars.Average(b => b.Range);
                writer.WriteLine($"Average volume per bar: {avgVolume:F0}");
                writer.WriteLine($"Average range per bar: {avgRange:F2}");
                writer.WriteLine($"Bullish bars: {minuteBars.Count(b => b.IsBullish):N0} ({(double)minuteBars.Count(b => b.IsBullish) / minuteBars.Count * 100:F1}%)");
            }
            writer.WriteLine();
            
            // Range ATR bars summary
            writer.WriteLine("RANGE ATR BARS SUMMARY:");
            writer.WriteLine($"Total Range ATR bars: {rangeATRBars.Count:N0}");
            if (rangeATRBars.Count > 0)
            {
                var avgATR = rangeATRBars.Average(b => b.ATRValue);
                var avgTicks = rangeATRBars.Average(b => b.TickCount);
                var avgDuration = rangeATRBars.Average(b => (b.LastUpdateTime - b.BarStartTime).TotalSeconds);
                writer.WriteLine($"Average ATR value: {avgATR:F2}");
                writer.WriteLine($"Average ticks per bar: {avgTicks:F0}");
                writer.WriteLine($"Average bar duration: {avgDuration:F1} seconds");
                writer.WriteLine($"Bullish bars: {rangeATRBars.Count(b => b.IsBullish):N0} ({(double)rangeATRBars.Count(b => b.IsBullish) / rangeATRBars.Count * 100:F1}%)");
            }
            writer.WriteLine();
            
            writer.WriteLine("=== END OF SUMMARY ===");
            
            Console.WriteLine($"Summary report saved to {filePath}");
        }
    }
}