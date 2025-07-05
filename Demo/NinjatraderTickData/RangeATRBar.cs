using System;
using System.Collections.Generic;
using System.Linq;
using CustomAlgo.Models;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo.NinjatraderTickData
{
    /// <summary>
    /// Custom Range ATR Bar implementation similar to P_Range from Ninjatrader
    /// </summary>
    public class RangeATRBar : OHLCData
    {
        public double ATRValue { get; set; }
        public int TickCount { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public double RangeThreshold { get; set; }
        public int MinTicks { get; set; }
        public TimeSpan MinTimeSpan { get; set; }
        public DateTime BarStartTime { get; set; }

        public RangeATRBar()
        {
            BarStartTime = DateTime.Now;
            LastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// Checks if the bar should be completed based on range and criteria
        /// </summary>
        public bool ShouldCompleteBar(double newPrice, double tickSize)
        {
            if (RangeThreshold <= 0) return false;

            var currentHigh = Math.Max(High, newPrice);
            var currentLow = Math.Min(Low, newPrice);
            var currentRange = currentHigh - currentLow;
            var rangeInTicks = currentRange / tickSize;

            // Check if range threshold is exceeded and minimum criteria are met
            return rangeInTicks >= RangeThreshold && 
                   TickCount >= MinTicks && 
                   (DateTime.Now - BarStartTime) >= MinTimeSpan;
        }

        public void UpdateWithTick(double price, uint volume = 0)
        {
            UpdatePrice(price, volume);
            TickCount++;
            LastUpdateTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Aggregates tick data into Range ATR bars
    /// </summary>
    public class RangeATRBarAggregator
    {
        private readonly Dictionary<string, RangeATRBar> _currentBars = new();
        private readonly Dictionary<string, List<OHLCData>> _atrCalculationBars = new();
        private readonly List<RangeATRBar> _completedBars = new();
        
        private readonly int _atrLookBackBars;
        private readonly int _recalcBars;
        private readonly int _minTicks;
        private readonly TimeSpan _minTimeSpan;

        public event Action<RangeATRBar>? BarCompleted;

        public RangeATRBarAggregator(int atrLookBackBars = 14, int recalcBars = 2, int minTicks = 1, int minTimeSeconds = 1)
        {
            _atrLookBackBars = atrLookBackBars;
            _recalcBars = recalcBars;
            _minTicks = minTicks;
            _minTimeSpan = TimeSpan.FromSeconds(minTimeSeconds);
        }

        /// <summary>
        /// Processes a tick and updates Range ATR bars
        /// </summary>
        public void ProcessTick(NinjatraderTick tick, double tickSize = 0.01)
        {
            var symbol = tick.Symbol;
            
            if (!_currentBars.TryGetValue(symbol, out var currentBar))
            {
                // Create new bar
                currentBar = new RangeATRBar
                {
                    Open = tick.Price,
                    High = tick.Price,
                    Low = tick.Price,
                    Close = tick.Price,
                    Volume = tick.Volume,
                    Timestamp = tick.Timestamp,
                    Symbol = symbol,
                    BarStartTime = tick.Timestamp,
                    LastUpdateTime = tick.Timestamp,
                    MinTicks = _minTicks,
                    MinTimeSpan = _minTimeSpan
                };

                // Calculate ATR and set range threshold
                var atr = CalculateATR(symbol, tickSize);
                currentBar.ATRValue = atr;
                currentBar.RangeThreshold = atr > 0 ? atr : 10; // Default to 10 ticks if no ATR available

                _currentBars[symbol] = currentBar;
            }
            else
            {
                // Check if current bar should be completed
                if (currentBar.ShouldCompleteBar(tick.Price, tickSize))
                {
                    CompleteBar(currentBar);

                    // Create new bar
                    currentBar = new RangeATRBar
                    {
                        Open = tick.Price,
                        High = tick.Price,
                        Low = tick.Price,
                        Close = tick.Price,
                        Volume = tick.Volume,
                        Timestamp = tick.Timestamp,
                        Symbol = symbol,
                        BarStartTime = tick.Timestamp,
                        LastUpdateTime = tick.Timestamp,
                        MinTicks = _minTicks,
                        MinTimeSpan = _minTimeSpan
                    };

                    // Recalculate ATR periodically
                    if (_completedBars.Count % _recalcBars == 0)
                    {
                        var atr = CalculateATR(symbol, tickSize);
                        currentBar.ATRValue = atr;
                        currentBar.RangeThreshold = atr > 0 ? atr : currentBar.RangeThreshold;
                    }
                    else
                    {
                        currentBar.ATRValue = _completedBars.LastOrDefault()?.ATRValue ?? 10;
                        currentBar.RangeThreshold = currentBar.ATRValue;
                    }

                    _currentBars[symbol] = currentBar;
                }
                else
                {
                    // Update existing bar
                    currentBar.UpdateWithTick(tick.Price, tick.Volume);
                    currentBar.Timestamp = tick.Timestamp;
                }
            }
        }

        /// <summary>
        /// Completes all pending bars
        /// </summary>
        public void CompleteAllBars()
        {
            foreach (var bar in _currentBars.Values.ToList())
            {
                CompleteBar(bar);
            }
            _currentBars.Clear();
        }

        /// <summary>
        /// Gets all completed bars
        /// </summary>
        public List<RangeATRBar> GetCompletedBars()
        {
            return new List<RangeATRBar>(_completedBars);
        }

        private double CalculateATR(string symbol, double tickSize)
        {
            if (!_atrCalculationBars.ContainsKey(symbol))
            {
                _atrCalculationBars[symbol] = new List<OHLCData>();
            }

            var bars = _atrCalculationBars[symbol];
            
            // Add completed bars to ATR calculation
            foreach (var completedBar in _completedBars.Where(b => b.Symbol == symbol).TakeLast(_atrLookBackBars))
            {
                if (!bars.Any(b => b.Timestamp == completedBar.Timestamp))
                {
                    bars.Add(new OHLCData
                    {
                        Open = completedBar.Open,
                        High = completedBar.High,
                        Low = completedBar.Low,
                        Close = completedBar.Close,
                        Volume = completedBar.Volume,
                        Timestamp = completedBar.Timestamp,
                        Symbol = symbol
                    });
                }
            }

            // Keep only the required number of bars
            if (bars.Count > _atrLookBackBars)
            {
                bars.RemoveRange(0, bars.Count - _atrLookBackBars);
            }

            if (bars.Count < 2)
            {
                return 10; // Default range in ticks
            }

            // Calculate ATR
            double atrSum = 0;
            for (int i = 1; i < bars.Count; i++)
            {
                var currentBar = bars[i];
                var previousBar = bars[i - 1];
                
                var tr1 = currentBar.High - currentBar.Low;
                var tr2 = Math.Abs(currentBar.High - previousBar.Close);
                var tr3 = Math.Abs(currentBar.Low - previousBar.Close);
                
                var trueRange = Math.Max(tr1, Math.Max(tr2, tr3));
                atrSum += trueRange / tickSize; // Convert to ticks
            }

            return atrSum / (bars.Count - 1);
        }

        private void CompleteBar(RangeATRBar bar)
        {
            _completedBars.Add(bar);
            BarCompleted?.Invoke(bar);
            _currentBars.Remove(bar.Symbol);
        }
    }
}