using System;

namespace CustomAlgo.Utilities
{
    /// <summary>
    /// Helper class for time handling in IST (Indian Standard Time)
    /// All timestamps in the application should use IST for consistency with Indian markets
    /// </summary>
    public static class TimeHelper
    {
        /// <summary>
        /// Indian Standard Time zone info
        /// </summary>
        public static readonly TimeZoneInfo IST = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        /// <summary>
        /// Gets current time in IST
        /// </summary>
        public static DateTime NowIST => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IST);

        /// <summary>
        /// Converts UTC time to IST
        /// </summary>
        /// <param name="utcTime">UTC DateTime</param>
        /// <returns>DateTime in IST</returns>
        public static DateTime ToIST(DateTime utcTime)
        {
            if (utcTime.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, IST);
            
            // Assume unspecified time is already in IST
            if (utcTime.Kind == DateTimeKind.Unspecified)
                return utcTime;
            
            // Convert local time to UTC first, then to IST
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime.ToUniversalTime(), IST);
        }

        /// <summary>
        /// Converts IST time to UTC
        /// </summary>
        /// <param name="istTime">IST DateTime</param>
        /// <returns>DateTime in UTC</returns>
        public static DateTime ToUTC(DateTime istTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(istTime, IST);
        }

        /// <summary>
        /// Creates DateTime in IST timezone
        /// </summary>
        /// <param name="year">Year</param>
        /// <param name="month">Month</param>
        /// <param name="day">Day</param>
        /// <param name="hour">Hour</param>
        /// <param name="minute">Minute</param>
        /// <param name="second">Second</param>
        /// <returns>DateTime in IST</returns>
        public static DateTime CreateIST(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Formats DateTime for display in IST
        /// </summary>
        /// <param name="dateTime">DateTime to format</param>
        /// <param name="format">Format string (default: yyyy-MM-dd HH:mm:ss IST)</param>
        /// <returns>Formatted string with IST indicator</returns>
        public static string FormatIST(DateTime dateTime, string format = "yyyy-MM-dd HH:mm:ss")
        {
            var istTime = dateTime.Kind == DateTimeKind.Utc ? ToIST(dateTime) : dateTime;
            return $"{istTime.ToString(format)} IST";
        }

        /// <summary>
        /// Formats DateTime for logging in IST
        /// </summary>
        /// <param name="dateTime">DateTime to format</param>
        /// <returns>Formatted string for logging</returns>
        public static string FormatForLogging(DateTime dateTime)
        {
            return FormatIST(dateTime, "yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// Checks if current IST time is within market hours
        /// NSE market hours: 9:15 AM to 3:30 PM IST
        /// </summary>
        /// <returns>True if within market hours</returns>
        public static bool IsMarketOpen()
        {
            var now = NowIST;
            var marketStart = CreateIST(now.Year, now.Month, now.Day, 9, 15, 0);
            var marketEnd = CreateIST(now.Year, now.Month, now.Day, 15, 30, 0);
            
            return now >= marketStart && now <= marketEnd;
        }

        /// <summary>
        /// Gets time remaining until market opens (if closed) or closes (if open)
        /// </summary>
        /// <returns>TimeSpan until next market event</returns>
        public static TimeSpan TimeToNextMarketEvent()
        {
            var now = NowIST;
            var today = now.Date;
            var marketStart = CreateIST(today.Year, today.Month, today.Day, 9, 15, 0);
            var marketEnd = CreateIST(today.Year, today.Month, today.Day, 15, 30, 0);

            if (now < marketStart)
            {
                // Market hasn't opened today
                return marketStart - now;
            }
            else if (now <= marketEnd)
            {
                // Market is open, time until close
                return marketEnd - now;
            }
            else
            {
                // Market closed today, time until tomorrow's open
                var tomorrowStart = marketStart.AddDays(1);
                return tomorrowStart - now;
            }
        }

        /// <summary>
        /// Gets a human-readable string for market status
        /// </summary>
        /// <returns>Market status string</returns>
        public static string GetMarketStatus()
        {
            if (IsMarketOpen())
            {
                var timeToClose = TimeToNextMarketEvent();
                return $"Market OPEN - Closes in {timeToClose.Hours}h {timeToClose.Minutes}m";
            }
            else
            {
                var timeToOpen = TimeToNextMarketEvent();
                return $"Market CLOSED - Opens in {timeToOpen.Hours}h {timeToOpen.Minutes}m";
            }
        }

        /// <summary>
        /// Creates a DateTime with explicit IST kind for JSON serialization
        /// </summary>
        /// <param name="dateTime">Source DateTime</param>
        /// <returns>DateTime with proper timezone handling</returns>
        public static DateTime EnsureIST(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return ToIST(dateTime);
            
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
        }
    }
}