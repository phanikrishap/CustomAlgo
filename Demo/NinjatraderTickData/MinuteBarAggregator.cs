using System;
using System.Collections.Generic;
using CustomAlgo.Models;
using CustomAlgo.Utilities;

namespace CustomAlgo.Demo.NinjatraderTickData
{
    /// <summary>
    /// Aggregates tick data into minute bars
    /// </summary>
    public class MinuteBarAggregator
    {
        private readonly Dictionary<string, OHLCData> _currentBars = new();
        private readonly List<OHLCData> _completedBars = new();

        public event Action<OHLCData>? BarCompleted;

        /// <summary>
        /// Processes a tick and updates minute bars
        /// </summary>
        public void ProcessTick(NinjatraderTick tick)
        {
            var minuteKey = GetMinuteKey(tick.Timestamp, tick.Symbol);
            
            if (_currentBars.TryGetValue(minuteKey, out var currentBar))
            {
                // Update existing bar
                currentBar.UpdatePrice(tick.Price, tick.Volume);
            }
            else
            {
                // Check if we need to complete the previous bar
                var previousBar = GetPreviousBar(tick.Symbol);
                if (previousBar != null && previousBar.Timestamp < GetMinuteTimestamp(tick.Timestamp))
                {
                    CompleteBar(previousBar);
                }

                // Create new bar
                var newBar = new OHLCData
                {
                    Open = tick.Price,
                    High = tick.Price,
                    Low = tick.Price,
                    Close = tick.Price,
                    Volume = tick.Volume,
                    Timestamp = GetMinuteTimestamp(tick.Timestamp),
                    Symbol = tick.Symbol
                };

                _currentBars[minuteKey] = newBar;
            }
        }

        /// <summary>
        /// Completes all pending bars
        /// </summary>
        public void CompleteAllBars()
        {
            foreach (var bar in _currentBars.Values)
            {
                CompleteBar(bar);
            }
            _currentBars.Clear();
        }

        /// <summary>
        /// Gets all completed bars
        /// </summary>
        public List<OHLCData> GetCompletedBars()
        {
            return new List<OHLCData>(_completedBars);
        }

        private string GetMinuteKey(DateTime timestamp, string symbol)
        {
            var minuteTime = GetMinuteTimestamp(timestamp);
            return $"{symbol}_{minuteTime:yyyyMMdd_HHmm}";
        }

        private DateTime GetMinuteTimestamp(DateTime timestamp)
        {
            return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                               timestamp.Hour, timestamp.Minute, 0, DateTimeKind.Unspecified);
        }

        private OHLCData? GetPreviousBar(string symbol)
        {
            OHLCData? previousBar = null;
            foreach (var kvp in _currentBars)
            {
                if (kvp.Key.StartsWith(symbol + "_"))
                {
                    if (previousBar == null || kvp.Value.Timestamp > previousBar.Timestamp)
                    {
                        previousBar = kvp.Value;
                    }
                }
            }
            return previousBar;
        }

        private void CompleteBar(OHLCData bar)
        {
            _completedBars.Add(bar);
            BarCompleted?.Invoke(bar);
            
            // Remove from current bars
            var keyToRemove = GetMinuteKey(bar.Timestamp, bar.Symbol);
            _currentBars.Remove(keyToRemove);
        }
    }
}