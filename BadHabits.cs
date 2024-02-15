using System.Drawing;
using System.Diagnostics;
using ATAS.DataFeedsCore;
using ATAS.Indicators;
using ATAS.Indicators.Technical;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using String = System.String;
using Color = System.Drawing.Color;
using OFT.Rendering.Control;
using ATAS.Indicators.Drawing;
using System.Windows.Media;

public class BadHabits : ATAS.Strategies.Chart.ChartStrategy
{
    #region VARIABLES

    private Rectangle greenButton = new Rectangle();
    private Rectangle redButton = new Rectangle();
    private Color cgreen = Color.Green;
    private Color cred = Color.Red;
    private const String sVersion = "Beta 1.0";
    private Stopwatch clock = new Stopwatch();
    private bool buyMe;
    private bool sellMe;

    #endregion

    #region INDICATORS

    private readonly SMA sma = new SMA() { Period = 6 };
    private readonly ParabolicSAR _psar = new ParabolicSAR();
    private readonly EMA fastEma = new EMA() { Period = 20 };
    private readonly EMA slowEma = new EMA() { Period = 40 };
    private readonly SuperTrend _st = new SuperTrend() { Period = 10, Multiplier = 1m };
    private readonly KAMA _kama9 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 9 };
    private readonly MACD _macd = new MACD() { ShortPeriod = 3, LongPeriod = 10, SignalPeriod = 16 };

    #endregion

    #region RENDER CONTEXT

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        var font = new RenderFont("Calibri", 10);
        var fontB = new RenderFont("Calibri", 14, FontStyle.Bold);
        int upY = 50;
        int upX = 50;
        var txt = String.Empty;

        if (buyMe || CurrentPosition < 0)
        {
            greenButton = new Rectangle(ChartArea.Width - 200, 350, 90, 50);
            context.DrawRectangle(RenderPens.Lime, greenButton);
            context.FillRectangle(cgreen, greenButton);
            context.DrawString("   BUY", fontB, Color.Black, greenButton);
        }

        if (sellMe || CurrentPosition > 0)
        { 
            redButton = new Rectangle(ChartArea.Width - 200, 250, 90, 50);
            context.DrawRectangle(RenderPens.Red, redButton);
            context.FillRectangle(cred, redButton);
            context.DrawString("   SELL", fontB, Color.White, redButton);
        }

        TimeSpan t = TimeSpan.FromMilliseconds(clock.ElapsedMilliseconds);
        String an = String.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        txt = $"Bad Habits ACTIVE on {TradingManager.Portfolio.AccountID} " + " (" + an + ")";
        context.DrawString(txt, fontB, Color.Green, upX, upY);
        if (!clock.IsRunning)
            clock.Start();

        var tsize = context.MeasureString(txt, fontB);
        upY += tsize.Height + 6;

        if (TradingManager.Portfolio != null && TradingManager.Position != null)
        {
            txt = $"{TradingManager.MyTrades.Count()} trades, with PNL: {TradingManager.Position.RealizedPnL}";
            context.DrawString(txt, font, Color.White, upX, upY);
        }
    }

    #endregion

    private ValueDataSeries _kamanine = new("KAMA NINE") { VisualType = VisualMode.Line, Color = DefaultColors.Yellow.Convert(), Width = 4 };

    public BadHabits()
    {
        DataSeries[0] = _kamanine;

        EnableCustomDrawing = true;
        Add(_psar);
        Add(_st);
        Add(_kama9);
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar < 6)
            return;

        var pbar = bar - 1;

        var candle = GetCandle(pbar);
        value = candle.Close;
        var p1C = GetCandle(pbar - 1);
        var p2C = GetCandle(pbar - 2);
        var p3C = GetCandle(pbar - 3);
        var p4C = GetCandle(pbar - 4);

        #region INDICATOR CALCULATIONS

        fastEma.Calculate(pbar, value);
        slowEma.Calculate(pbar, value);
        _macd.Calculate(pbar, value);

        var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[pbar];
        var fast = ((ValueDataSeries)fastEma.DataSeries[0])[pbar];
        var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[pbar - 1];
        var slow = ((ValueDataSeries)slowEma.DataSeries[0])[pbar];
        var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[pbar - 1];
        var psar = ((ValueDataSeries)_psar.DataSeries[0])[pbar];
        var m1 = ((ValueDataSeries)_macd.DataSeries[0])[pbar];
        var m2 = ((ValueDataSeries)_macd.DataSeries[1])[pbar];
        var m3 = ((ValueDataSeries)_macd.DataSeries[2])[pbar];

        var t1 = ((fast - slow) - (fastM - slowM)) * 150;

        var psarBuy = (psar < candle.Close);
        var psarSell = (psar > candle.Close);
        var macdUp = (m1 > m2);
        var macdDown = (m1 < m2);

        if (psarBuy && macdUp && t1 > 0 && candle.Close > kama9)
            cgreen = Color.Lime;
        else
            cgreen = Color.Green;

        if (psarSell && macdDown && t1 < 0 && candle.Close < kama9)
            cred = Color.Red;
        else
            cred = Color.Maroon;

        buyMe = (macdUp || t1 > 0) && psarBuy;
        sellMe = (macdDown || t1 < 0) && psarSell;

        #endregion

        decimal _tick = ChartInfo.PriceChartContainer.Step;

        bool BuyMe = (macdUp || t1 > 0) && psarBuy && CurrentPosition == 0;
        bool SellMe = (macdDown || t1 < 0) && psarSell && CurrentPosition == 0;

    }

    #region POSITION METHODS

    private void OpenPosition(String sReason, OrderDirections direction)
    {
        if (CurrentPosition > 0 && direction == OrderDirections.Buy)
            return;
        if (CurrentPosition < 0 && direction == OrderDirections.Sell)
            return;

        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = direction,
            Type = OrderTypes.Market,
            QuantityToFill = 1, // GetOrderVolume(),
            Comment = ""
        };
        OpenOrder(order);
    }

    private void CloseCurrentPosition(String s, int bar)
    {
        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = CurrentPosition > 0 ? OrderDirections.Sell : OrderDirections.Buy,
            Type = OrderTypes.Market,
            QuantityToFill = Math.Abs(CurrentPosition),
            Comment = "Position closed, reason: " + s
        };
        OpenOrder(order);
    }

    #endregion

    #region MISC METHODS

    private bool IsPointInsideRectangle(Rectangle rectangle, Point point)
    {
        return point.X >= rectangle.X && point.X <= rectangle.X + rectangle.Width && point.Y >= rectangle.Y && point.Y <= rectangle.Y + rectangle.Height;
    }

    public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
    {
        if (buyMe || CurrentPosition < 0)
        {
            if (e.Button == RenderControlMouseButtons.Left && IsPointInsideRectangle(greenButton, e.Location))
                OpenPosition("", OrderDirections.Buy);
        }

        if (sellMe || CurrentPosition > 0)
        {
            if (e.Button == RenderControlMouseButtons.Left && IsPointInsideRectangle(redButton, e.Location))
                OpenPosition("", OrderDirections.Sell);
        }

        return false;
    }

    #endregion

}

