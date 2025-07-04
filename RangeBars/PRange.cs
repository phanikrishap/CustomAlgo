
using NinjaTrader.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NinjaTrader.NinjaScript.BarsTypes
{
  public class P_Range : BarsType
  {
    private int LookBackBars = 2;
    private int RecalcBars = 2;
    private int PRangeValue = -1;
    private DateTime LastNewBarTime = DateTime.MinValue;
    private DateTime LastDateCalculated = new DateTime();
    private List<P_Range.OHLCBar> SurrogateBars = new List<P_Range.OHLCBar>();
    private int RecalcPRangeCount;
    private int PRangeCalcBar;
    private int NumTicksInBar;
    private TimeSpan MinTimeSecondsSpan;
    private DateTime tempTime;
    private double PRangeValueNew;
    private DateTime curSessionStartTime;
    private DateTime curSessionEndTime;

    protected override void OnStateChange()
    {
      if (this.State == State.SetDefaults)
      {
        this.Description = "P_Range bars";
        this.Name = "P_Range"; //nameof (P_Range);
        this.BarsPeriod = new BarsPeriod()
        {
          BarsPeriodType = (BarsPeriodType) 6600,
          BarsPeriodTypeName = "P_Range(6600)",
          Value = 1
        };
        this.BuiltFrom = BarsPeriodType.Tick;
        this.DaysToLoad = 20;
        this.IsIntraday = true;
        this.IsTimeBased = false;
      }
      else
      {
        if (this.State != State.Configure)
          return;
        this.Properties.Remove(this.Properties.Find("BaseBarsPeriodType", true));
        this.Properties.Remove(this.Properties.Find("PointAndFigurePriceType", true));
        this.Properties.Remove(this.Properties.Find("ReversalType", true));
        this.SetPropertyName("Value", "MinuteValue");
        this.SetPropertyName("Value2", "MinSeconds");
        this.SetPropertyName("BaseBarsPeriodValue", "MinTicks");
        this.PRangeValueNew = 0.0;
        this.curSessionEndTime = DateTime.MinValue;
        this.LastNewBarTime = DateTime.MinValue;
      }
    }

    public override int GetInitialLookBackDays(
      BarsPeriod barsPeriod,
      TradingHours tradingHours,
      int barsBack)
    {
      return 1;
    }

    protected override void OnDataPoint(
      Bars bars,
      double open,
      double high,
      double low,
      double close,
      DateTime time,
      long volume,
      bool isBar,
      double bid,
      double ask)
    {
      if (this.SessionIterator == null)
        this.SessionIterator = new SessionIterator(bars);
      bool flag = this.SessionIterator.IsNewSession(time, isBar);
			if (flag)
			{
				this.SessionIterator.GetNextSession(time, isBar);

				string str = "Flag: " + flag + "  ActualSessionBegin:  " + this.SessionIterator.ActualSessionBegin + "  ActualSessionEnd: " + this.SessionIterator.ActualSessionEnd + "\n" +
					"BarsPeriod Value: " + bars.BarsPeriod.Value + "  Value2: " + bars.BarsPeriod.Value2 + "  Value3: " + bars.BarsPeriod.BaseBarsPeriodValue + "\n" +
                    "IsResetOnNewTradingDay: " + bars.IsResetOnNewTradingDay;

				Print(str);
            }

			if (this.SurrogateBars.Count > 0 && time < this.SurrogateBars[this.SurrogateBars.Count - 1].Date)
			{
				int num = this.SurrogateBars.Count - 1;
				for (int index = this.SurrogateBars.Count - 1; index >= 0 && this.SurrogateBars[index].Date >= time; --index)
					num = index;
				if (num != this.SurrogateBars.Count - 1)
				{
					this.SurrogateBars = this.SurrogateBars.GetRange(0, num + 1);
					this.PRangeCalcBar = 0;
					this.PRangeValue = -1;
				}
			}

      if (this.SurrogateBars.Count == 0)
	      {
	        int val2 = (int) (60.0 / (double) this.LookBackBars + 0.5);
	        this.LookBackBars = Math.Max(this.LookBackBars, val2);
	        this.RecalcBars = Math.Min(this.RecalcBars, val2 * 2);
	        this.MinTimeSecondsSpan = new TimeSpan(0, 0, 0, bars.BarsPeriod.Value2);
	        DateTime actualSessionBegin = this.SessionIterator.ActualSessionBegin;
	        this.SurrogateBars = new List<P_Range.OHLCBar>();
	        this.SurrogateBars.Add(new P_Range.OHLCBar(open, high, low, close, P_Range.TimeToBarTime(bars, time, new DateTime(actualSessionBegin.Year, actualSessionBegin.Month, actualSessionBegin.Day, actualSessionBegin.Hour, actualSessionBegin.Minute, 0), bars.BarsPeriod.Value * 60)));
	        this.PRangeCalcBar = 0;
	      }
      else
	      {
	        int num = bars.BarsPeriod.Value;
	        DateTime date = this.SurrogateBars.Last<P_Range.OHLCBar>().Date;
	        if (time >= date)
		        {
		          if (num > 1 && time < date.AddMinutes((double) num) || num == 1 && time <= date.AddMinutes((double) num))
		            this.SurrogateBars.Last<P_Range.OHLCBar>().UpdateOHLC(high, low, close, new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0));
		          else
		            this.SurrogateBars.Add(new P_Range.OHLCBar(open, high, low, close, time));
		        }
	      }
      if (this.PRangeValue == -1 || this.SurrogateBars.IndexOf(this.SurrogateBars.Last<P_Range.OHLCBar>()) - this.PRangeCalcBar >= this.RecalcBars)
	      {
	        this.PRangeValue = this.RecalculateRange(bars.Instrument.MasterInstrument.TickSize);
	        this.PRangeCalcBar = this.SurrogateBars.IndexOf(this.SurrogateBars.Last<P_Range.OHLCBar>());
	      }
      if (this.PRangeValue == -1)
        return;
      if (bars.Count == 0 || bars.IsResetOnNewTradingDay && flag)
	      {
	        this.AddBar(bars, open, high, low, close, time, volume);
	        this.PRangeValue = this.RecalculateRange(bars.Instrument.MasterInstrument.TickSize);
	        this.PRangeCalcBar = this.SurrogateBars.IndexOf(this.SurrogateBars.Last<P_Range.OHLCBar>());
	        this.SetNewBarVariables(time);
	      }
      else
	      {
	        double num = Math.Floor(10000000.0 * (double) this.PRangeValue * bars.Instrument.MasterInstrument.TickSize) / 10000000.0;
	        bars.GetOpen(bars.Count - 1);
	        double high1 = bars.GetHigh(bars.Count - 1);
	        double low1 = bars.GetLow(bars.Count - 1);
	        bars.GetClose(bars.Count - 1);
	        if (this.CheckMinCriteria(time, bars.BarsPeriod.BaseBarsPeriodValue) && bars.Instrument.MasterInstrument.Compare(close, low1 + num) > 0)
	        {
	          this.AddBar(bars, open, high, low, close, time, volume);
	          this.SetNewBarVariables(time);
	        }
	        else if (this.CheckMinCriteria(time, bars.BarsPeriod.BaseBarsPeriodValue) && bars.Instrument.MasterInstrument.Compare(high1 - num, close) > 0)
	        {
	          this.AddBar(bars, open, high, low, close, time, volume);
	          this.SetNewBarVariables(time);
	        }
	        else
	        {
	          ++this.NumTicksInBar;
	          this.UpdateBar(bars, high > high1 ? high : high1, low < low1 ? low : low1, close, time, volume);
	        }
	      }
      bars.LastPrice = close;
    }

    public override object Clone()
	    {
	      P_Range P_Range = (P_Range) base.Clone();
	      P_Range.PRangeCalcBar = this.PRangeCalcBar;
	      P_Range.PRangeValue = this.PRangeValue;
	      P_Range.SurrogateBars = this.SurrogateBars;
	      return (object) P_Range;
	    }

    public override void ApplyDefaultBasePeriodValue(BarsPeriod period)
	    {
	      period.BaseBarsPeriodValue = 1;
	    }

    public override void ApplyDefaultValue(BarsPeriod period)
	    {
	      period.Value = 1;
	      period.Value2 = 1;
	      period.BaseBarsPeriodValue = 1;
	    }

    public override string ChartLabel(DateTime dateTime)
	    {
	      return dateTime.ToString();
	    }

    public override double GetPercentComplete(Bars bars, DateTime now)
	    {
	      throw new ApplicationException("GetPercentComplete not supported in " + this.DisplayName);
	    }

    private static DateTime TimeToBarTime(
      Bars bars,
      DateTime time,
      DateTime periodStart,
      int periodValue)
	    {
	      SessionIterator sessionIterator = new SessionIterator(bars);
	      TimeSpan timeSpan = new TimeSpan(0L);
	      DateTime dateTime = periodStart.AddSeconds(Math.Ceiling(Math.Ceiling(Math.Max(0.0, time.AddSeconds(periodValue > 1 ? 1.0 : 0.0).Subtract(periodStart).TotalSeconds)) / (double) periodValue) * (double) periodValue);
	      if (sessionIterator.ActualSessionEnd - sessionIterator.ActualSessionEnd > timeSpan && dateTime > sessionIterator.ActualSessionEnd)
	        dateTime = sessionIterator.ActualSessionEnd;
	      return dateTime;
	    }

    private int RecalculateRange(double tickSize)
	    {
	      int num1 = this.SurrogateBars.IndexOf(this.SurrogateBars.Last<P_Range.OHLCBar>());
	      if (num1 < this.LookBackBars)
	        return -1;
	      double num2 = 0.0;
	      for (int index = num1; index >= num1 - this.LookBackBars; --index)
	      {
	        double high = this.SurrogateBars[index].High;
	        double low = this.SurrogateBars[index].Low;
	        num2 += (high - low) / tickSize;
	      }
	      return (int) (num2 / (double) this.LookBackBars + 0.5);
	    }

    private void SetNewBarVariables(DateTime Time)
	    {
	      this.LastNewBarTime = Time;
	      this.NumTicksInBar = 0;
	    }

    private bool CheckMinCriteria(DateTime Time, int TickThreshold)
	    {
	      bool flag = false;
	      if (this.NumTicksInBar >= TickThreshold && Time.Subtract(this.LastNewBarTime) >= this.MinTimeSecondsSpan)
	        flag = true;
	      return flag;
	    }

    private void DebugPrint(string message)
	    {
	      string path = "C:\\Temp\\NT8DebugOutput.txt";
	      if (!File.Exists(path))
	      {
	        using (StreamWriter text = File.CreateText(path))
	          text.WriteLine(message);
	      }
	      else
	      {
	        using (StreamWriter streamWriter = File.AppendText(path))
	          streamWriter.WriteLine(message);
	      }
	    }

    private void RemoveExcess(DateTime time)
	    {
	      List<P_Range.OHLCBar> ohlcBarList = new List<P_Range.OHLCBar>();
	      foreach (P_Range.OHLCBar surrogateBar in this.SurrogateBars)
	      {
	        if (surrogateBar.Date <= time)
	          ohlcBarList.Add(surrogateBar);
	      }
	      this.SurrogateBars = ohlcBarList;
	    }

    private class OHLCBar
	    {
	      public double Open;
	      public double High;
	      public double Low;
	      public double Close;
	      public DateTime Date;

	      public OHLCBar()
	      {
	        this.Open = double.NaN;
	        this.High = double.NaN;
	        this.Low = double.NaN;
	        this.Close = double.NaN;
	        this.Date = new DateTime();
	      }

	      public OHLCBar(double open = 0.0, double high = 0.0, double low = 0.0, double close = 0.0, DateTime date = default (DateTime))
	      {
	        this.Open = open;
	        this.High = high;
	        this.Low = low;
	        this.Close = close;
	        this.Date = date;
	      }

      public void UpdateOHLC(double high = 0.0, double low = 0.0, double close = 0.0, DateTime date = default (DateTime))
	      {
	        if (high > this.High)
	          this.High = high;
	        if (low < this.Low)
	          this.Low = low;
	        this.Close = close;
	        this.Date = date;
	      }
    }
  }
}
