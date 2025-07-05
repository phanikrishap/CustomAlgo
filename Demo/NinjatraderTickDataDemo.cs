using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomAlgo.Demo.NinjatraderTickData;
using CustomAlgo.Models;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo
{
    /// <summary>
    /// Demo script that processes Ninjatrader tick data and converts it to minute bars and Range ATR bars
    /// </summary>
    public class NinjatraderTickDataDemo
    {
        private const string DATA_FOLDER = @"Demo\NinjatraderTickData";
        private const string OUTPUT_FOLDER = @"Demo\Output";
        private const double TICK_SIZE = 0.01; // NIFTY tick size
        
        /// <summary>
        /// Main entry point for standalone testing
        /// </summary>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== NINJATRADER TICK DATA DEMO - STANDALONE ===");
            RunDemo();
            Console.WriteLine("\nDemo completed. Press any key to exit...");
            Console.ReadKey();
        }
        
        public static void RunDemo()
        {
            try
            {
                Console.WriteLine("=== NINJATRADER TICK DATA PROCESSING DEMO ===");
                Console.WriteLine($"Started at: {TimeHelper.FormatIST(DateTime.Now)}");
                Console.WriteLine();

                // Ensure output directory exists
                var outputPath = Path.Combine(Directory.GetCurrentDirectory(), OUTPUT_FOLDER);
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                // Load tick data
                var ticks = LoadTickData();
                Console.WriteLine($"Loaded {ticks.Count:N0} ticks");

                if (ticks.Count == 0)
                {
                    Console.WriteLine("No tick data found. Please ensure NIFTY_I.Last.txt exists in the NinjatraderTickData folder.");
                    return;
                }

                // Process ticks into minute bars
                Console.WriteLine("\nProcessing minute bars...");
                var minuteBars = ProcessMinuteBars(ticks);
                Console.WriteLine($"Generated {minuteBars.Count:N0} minute bars");

                // Process ticks into Range ATR bars
                Console.WriteLine("\nProcessing Range ATR bars...");
                var rangeATRBars = ProcessRangeATRBars(ticks);
                Console.WriteLine($"Generated {rangeATRBars.Count:N0} Range ATR bars");

                // Export results to CSV
                Console.WriteLine("\nExporting results to CSV files...");
                ExportResults(ticks, minuteBars, rangeATRBars, outputPath);

                // Display summary
                DisplaySummary(ticks, minuteBars, rangeATRBars);

                Console.WriteLine($"\nDemo completed at: {TimeHelper.FormatIST(DateTime.Now)}");
                Console.WriteLine($"Output files saved to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running demo: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static List<NinjatraderTick> LoadTickData()
        {
            var ticks = new List<NinjatraderTick>();
            
            // Try multiple possible paths
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), DATA_FOLDER, "NIFTY_I.Last.txt"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", DATA_FOLDER, "NIFTY_I.Last.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DATA_FOLDER, "NIFTY_I.Last.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", DATA_FOLDER, "NIFTY_I.Last.txt")
            };
            
            string dataPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    dataPath = path;
                    break;
                }
            }
            
            if (dataPath == null)
            {
                Console.WriteLine("Tick data file not found in any of the expected locations:");
                foreach (var path in possiblePaths)
                {
                    Console.WriteLine($"  - {path}");
                }
                return ticks;
            }
            
            Console.WriteLine($"Found tick data file: {dataPath}");

            var lines = File.ReadAllLines(dataPath);
            int lineNumber = 0;
            int errorCount = 0;

            foreach (var line in lines)
            {
                lineNumber++;
                try
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var tick = NinjatraderTick.ParseLine(line, "NIFTY");
                    ticks.Add(tick);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount <= 5) // Show first 5 errors
                    {
                        Console.WriteLine($"Error parsing line {lineNumber}: {ex.Message}");
                        Console.WriteLine($"Line content: {line}");
                    }
                }
            }

            if (errorCount > 0)
            {
                Console.WriteLine($"Total parsing errors: {errorCount}");
            }

            return ticks.OrderBy(t => t.Timestamp).ToList();
        }

        private static List<OHLCData> ProcessMinuteBars(List<NinjatraderTick> ticks)
        {
            var aggregator = new MinuteBarAggregator();
            var completedBars = new List<OHLCData>();

            // Subscribe to bar completion events
            aggregator.BarCompleted += bar =>
            {
                completedBars.Add(bar);
                if (completedBars.Count % 100 == 0)
                {
                    Console.WriteLine($"Processed {completedBars.Count} minute bars...");
                }
            };

            // Process all ticks
            foreach (var tick in ticks)
            {
                aggregator.ProcessTick(tick);
            }

            // Complete any remaining bars
            aggregator.CompleteAllBars();

            return aggregator.GetCompletedBars();
        }

        private static List<RangeATRBar> ProcessRangeATRBars(List<NinjatraderTick> ticks)
        {
            // Configure Range ATR parameters
            var atrLookBackBars = 14;  // ATR calculation period
            var recalcBars = 5;        // Recalculate ATR every 5 bars
            var minTicks = 3;          // Minimum ticks per bar
            var minTimeSeconds = 2;    // Minimum time per bar

            var aggregator = new RangeATRBarAggregator(atrLookBackBars, recalcBars, minTicks, minTimeSeconds);
            var completedBars = new List<RangeATRBar>();

            // Subscribe to bar completion events
            aggregator.BarCompleted += bar =>
            {
                completedBars.Add(bar);
                if (completedBars.Count % 50 == 0)
                {
                    Console.WriteLine($"Processed {completedBars.Count} Range ATR bars...");
                }
            };

            // Process all ticks
            foreach (var tick in ticks)
            {
                aggregator.ProcessTick(tick, TICK_SIZE);
            }

            // Complete any remaining bars
            aggregator.CompleteAllBars();

            return aggregator.GetCompletedBars();
        }

        private static void ExportResults(List<NinjatraderTick> ticks, 
                                        List<OHLCData> minuteBars, 
                                        List<RangeATRBar> rangeATRBars, 
                                        string outputPath)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // Export tick data
            var ticksFile = Path.Combine(outputPath, $"NIFTY_Ticks_{timestamp}.csv");
            CsvExporter.ExportTickData(ticks, ticksFile);

            // Export minute bars
            var minuteBarsFile = Path.Combine(outputPath, $"NIFTY_MinuteBars_{timestamp}.csv");
            CsvExporter.ExportMinuteBars(minuteBars, minuteBarsFile);

            // Export Range ATR bars
            var rangeATRBarsFile = Path.Combine(outputPath, $"NIFTY_RangeATRBars_{timestamp}.csv");
            CsvExporter.ExportRangeATRBars(rangeATRBars, rangeATRBarsFile);

            // Create summary report
            var summaryFile = Path.Combine(outputPath, $"NIFTY_Summary_{timestamp}.txt");
            CsvExporter.CreateSummaryReport(ticks, minuteBars, rangeATRBars, summaryFile);
        }

        private static void DisplaySummary(List<NinjatraderTick> ticks, 
                                         List<OHLCData> minuteBars, 
                                         List<RangeATRBar> rangeATRBars)
        {
            Console.WriteLine("\n=== PROCESSING SUMMARY ===");
            
            if (ticks.Count > 0)
            {
                var firstTick = ticks.First();
                var lastTick = ticks.Last();
                var duration = lastTick.Timestamp - firstTick.Timestamp;
                
                Console.WriteLine($"Tick Data:");
                Console.WriteLine($"  Count: {ticks.Count:N0}");
                Console.WriteLine($"  Duration: {duration.TotalHours:F1} hours");
                Console.WriteLine($"  Price Range: {ticks.Min(t => t.Price):F2} - {ticks.Max(t => t.Price):F2}");
                Console.WriteLine($"  Total Volume: {ticks.Sum(t => (long)t.Volume):N0}");
            }

            if (minuteBars.Count > 0)
            {
                Console.WriteLine($"\nMinute Bars:");
                Console.WriteLine($"  Count: {minuteBars.Count:N0}");
                Console.WriteLine($"  Avg Volume: {minuteBars.Average(b => b.Volume):F0}");
                Console.WriteLine($"  Avg Range: {minuteBars.Average(b => b.Range):F2}");
                Console.WriteLine($"  Bullish: {minuteBars.Count(b => b.IsBullish):N0} ({(double)minuteBars.Count(b => b.IsBullish) / minuteBars.Count * 100:F1}%)");
            }

            if (rangeATRBars.Count > 0)
            {
                Console.WriteLine($"\nRange ATR Bars:");
                Console.WriteLine($"  Count: {rangeATRBars.Count:N0}");
                Console.WriteLine($"  Avg ATR: {rangeATRBars.Average(b => b.ATRValue):F2}");
                Console.WriteLine($"  Avg Ticks: {rangeATRBars.Average(b => b.TickCount):F0}");
                Console.WriteLine($"  Avg Duration: {rangeATRBars.Average(b => (b.LastUpdateTime - b.BarStartTime).TotalSeconds):F1}s");
                Console.WriteLine($"  Bullish: {rangeATRBars.Count(b => b.IsBullish):N0} ({(double)rangeATRBars.Count(b => b.IsBullish) / rangeATRBars.Count * 100:F1}%)");
            }

            Console.WriteLine("\n=== END SUMMARY ===");
        }
    }
}