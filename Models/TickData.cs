using System;
using CustomAlgo.Utilities;

namespace CustomAlgo.Models
{
    /// <summary>
    /// Represents tick data received from Kite WebSocket
    /// </summary>
    public class TickData
    {
        /// <summary>
        /// Instrument token from Kite
        /// </summary>
        public uint InstrumentToken { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        public double LastPrice { get; set; }

        /// <summary>
        /// Opening price
        /// </summary>
        public double Open { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        public double High { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        public double Low { get; set; }

        /// <summary>
        /// Closing price (previous day)
        /// </summary>
        public double Close { get; set; }

        /// <summary>
        /// Volume traded
        /// </summary>
        public uint Volume { get; set; }

        /// <summary>
        /// Best bid price
        /// </summary>
        public double BidPrice { get; set; }

        /// <summary>
        /// Best ask price
        /// </summary>
        public double AskPrice { get; set; }

        /// <summary>
        /// Best bid quantity
        /// </summary>
        public uint BidQuantity { get; set; }

        /// <summary>
        /// Best ask quantity
        /// </summary>
        public uint AskQuantity { get; set; }

        /// <summary>
        /// Average traded price
        /// </summary>
        public double AveragePrice { get; set; }

        /// <summary>
        /// Open interest (for derivatives)
        /// </summary>
        public uint OpenInterest { get; set; }

        /// <summary>
        /// Net change from previous close
        /// </summary>
        public double NetChange { get; set; }

        /// <summary>
        /// Percentage change from previous close
        /// </summary>
        public double PercentageChange { get; set; }

        /// <summary>
        /// Timestamp when tick was received (in IST)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Indicates if this is full market depth data
        /// </summary>
        public bool IsFullData { get; set; }

        /// <summary>
        /// Market depth data (buy orders)
        /// </summary>
        public MarketDepth[] BuyOrders { get; set; }

        /// <summary>
        /// Market depth data (sell orders)
        /// </summary>
        public MarketDepth[] SellOrders { get; set; }

        /// <summary>
        /// Gets the symbol from configured instruments (if available)
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Gets the subscription mode for this tick
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Calculates the spread (difference between ask and bid)
        /// </summary>
        public double Spread => AskPrice - BidPrice;

        /// <summary>
        /// Gets the mid price (average of bid and ask)
        /// </summary>
        public double MidPrice => (BidPrice + AskPrice) / 2.0;

        /// <summary>
        /// Checks if tick data is valid for trading decisions
        /// </summary>
        public bool IsValid => LastPrice > 0 && Timestamp != default(DateTime);

        /// <summary>
        /// Converts tick data to OHLC format for range bar construction
        /// </summary>
        public OHLCData ToOHLC()
        {
            return new OHLCData
            {
                Open = LastPrice,
                High = LastPrice,
                Low = LastPrice,
                Close = LastPrice,
                Volume = Volume,
                Timestamp = Timestamp,
                InstrumentToken = InstrumentToken,
                Symbol = Symbol
            };
        }

        /// <summary>
        /// Returns a string representation of the tick data
        /// </summary>
        public override string ToString()
        {
            return $"[{InstrumentToken}] {Symbol} LTP: {LastPrice:F2} " +
                   $"OHLC: {Open:F2}/{High:F2}/{Low:F2}/{Close:F2} " +
                   $"Vol: {Volume} Time: {TimeHelper.FormatForLogging(Timestamp)}";
        }

        /// <summary>
        /// Returns a compact string representation for logging
        /// </summary>
        public string ToLogString()
        {
            return $"{Symbol ?? InstrumentToken.ToString()}: {LastPrice:F2} @ {TimeHelper.FormatForLogging(Timestamp)}";
        }
    }

    /// <summary>
    /// Represents market depth data
    /// </summary>
    public class MarketDepth
    {
        /// <summary>
        /// Price level
        /// </summary>
        public double Price { get; set; }

        /// <summary>
        /// Quantity at this price level
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Number of orders at this price level
        /// </summary>
        public uint Orders { get; set; }

        public override string ToString()
        {
            return $"{Price:F2} x {Quantity} ({Orders} orders)";
        }
    }

    /// <summary>
    /// OHLC data structure for range bar construction
    /// </summary>
    public class OHLCData
    {
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public uint Volume { get; set; }
        public DateTime Timestamp { get; set; }
        public uint InstrumentToken { get; set; }
        public string Symbol { get; set; }

        /// <summary>
        /// Updates OHLC with new price data
        /// </summary>
        public void UpdatePrice(double price, uint additionalVolume = 0)
        {
            if (price > High) High = price;
            if (price < Low) Low = price;
            Close = price;
            Volume += additionalVolume;
        }

        /// <summary>
        /// Gets the range (High - Low)
        /// </summary>
        public double Range => High - Low;

        /// <summary>
        /// Gets the body (Close - Open)
        /// </summary>
        public double Body => Math.Abs(Close - Open);

        /// <summary>
        /// Indicates if this is a bullish bar (Close > Open)
        /// </summary>
        public bool IsBullish => Close > Open;

        /// <summary>
        /// Indicates if this is a bearish bar (Close < Open)
        /// </summary>
        public bool IsBearish => Close < Open;

        public override string ToString()
        {
            return $"{Symbol} OHLC: {Open:F2}/{High:F2}/{Low:F2}/{Close:F2} " +
                   $"Range: {Range:F2} Vol: {Volume} @ {TimeHelper.FormatIST(Timestamp, "HH:mm:ss")}";
        }
    }
}