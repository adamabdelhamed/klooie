﻿namespace klooie;

public class ThreeMonthCarouselOptions : MonthCalendarOptions
{
    public float AnimationDuration { get; set; } = 350;
}

public class ThreeMonthCalendar : ProtectedConsolePanel
{
    public ThreeMonthCarouselOptions Options { get; private set; }

    private MonthCalendar left;
    private MonthCalendar center;
    private MonthCalendar right;

    private int seekLtLease;
    private Recyclable seekLt;

    private ConsoleControl leftPlaceHolder;
    private ConsoleControl centerPlaceHolder;
    private ConsoleControl rightPlaceHolder;

    public ThreeMonthCalendar(ThreeMonthCarouselOptions options = null)
    {
        options = options ?? new ThreeMonthCarouselOptions();
        this.Options = options;
        SetupInvisiblePlaceholders();
        BoundsChanged.Sync(Refresh, this);
        SetupKeyboardHandling();
    }

    private void SetupKeyboardHandling()
    {
        if (Options.AdvanceMonthBackwardKey == null || Options.AdvanceMonthForwardKey == null) return;
        this.CanFocus = true;

        this.KeyInputReceived.Subscribe(key =>
        {
            var back = Options.AdvanceMonthBackwardKey;
            var fw = Options.AdvanceMonthForwardKey;

            var backModifierMatch = back.Modifier == null || key.Modifiers.HasFlag(back.Modifier);
            if (key.Key == back.Key && backModifierMatch) Seek(false, Options.AnimationDuration);

            var fwModifierMatch = fw.Modifier == null || key.Modifiers.HasFlag(fw.Modifier);
            if (key.Key == fw.Key && fwModifierMatch) Seek(true, Options.AnimationDuration);

        }, this);
    }


    private async void Seek(bool forward, float duration) => await SeekAsync(forward, duration);


    public async Task<bool> SeekAsync(bool forward, float duration)
    {
        if (seekLt?.IsStillValid(seekLtLease) == true) return false;
        seekLt = this.CreateChildRecyclable(out seekLtLease);
        try
        {
            var thisMonth = new DateTime(Options.Year, Options.Month, 1);
            thisMonth = thisMonth.AddMonths(forward ? 1 : -1);
            this.Options.Month = thisMonth.Month;
            this.Options.Year = thisMonth.Year;
            var lastMonth = thisMonth.AddMonths(-1);
            var nextMonth = thisMonth.AddMonths(1);

            var leftDest = CalculateLeftDestination();
            var centerDest = CalculateCenterDestination();
            var rightDest = CalculateRightDestination();

            var tempMonth = !forward ? lastMonth : nextMonth;
            var temp = ProtectedPanel.Add(new MonthCalendar(new MonthCalendarOptions() { CustomizeContent = Options.CustomizeContent, MinMonth = Options.MinMonth, MaxMonth = Options.MaxMonth, AdvanceMonthBackwardKey = null, AdvanceMonthForwardKey = null, TodayHighlightColor = Options.TodayHighlightColor, Month = tempMonth.Month, Year = tempMonth.Year }));
            temp.Width = 2;
            temp.Height = 1;
            temp.X = !forward ? -temp.Width : Width + temp.Width;
            temp.Y = ConsoleMath.Round((Height - temp.Height) / 2f);
            var tempDest = !forward ? leftDest : rightDest;

            EasingFunction ease = EasingFunctions.EaseInOut;

            // new format
            var tempAnimation = temp.AnimateAsync(()=> tempDest, duration, ease,animationLifetime: seekLt);


            if (!forward)
            {
                var rightAnimationDest = new RectF(Width + 2, Height / 2, 2, 1);
                var centerAnimationDest = right.Bounds;
                var leftAnimationDest = center.Bounds;

                await Task.WhenAll
                (
                    right.AnimateAsync(() => rightAnimationDest, duration, ease, animationLifetime: seekLt),
                    center.AnimateAsync(() => centerAnimationDest, duration, ease, animationLifetime: seekLt),
                    left.AnimateAsync(() => leftAnimationDest, duration, ease, animationLifetime: seekLt),
                    tempAnimation
                );

                right.Dispose();
                right = center;
                center = left;
                left = temp;
            }
            else
            {
                var rightAnimationDest = center.Bounds;
                var centerAnimationDest = left.Bounds;
                var leftAnimationDest = new RectF(-2, Height / 2, 2, 1);

                await Task.WhenAll
                (
                    right.AnimateAsync(() => rightAnimationDest, duration, ease, animationLifetime: seekLt),
                    center.AnimateAsync(() => centerAnimationDest, duration, ease, animationLifetime: seekLt),
                    left.AnimateAsync(() => leftAnimationDest, duration, ease, animationLifetime: seekLt),
                    tempAnimation
                );

                left.Dispose();
                left = center;
                center = right;
                right = temp;

                left.Bounds = leftDest;
                center.Bounds = centerDest;
                right.Bounds = rightDest;

                await Task.Yield();
                left.Refresh();
                right.Refresh();
                center.Refresh();
            }
            return true;
        }
        finally
        {
            seekLt?.TryDispose();
            seekLt = null;
        }
    }


    private void Refresh()
    {
        if (Width == 0 || Height == 0) return;


        seekLt?.TryDispose();
        var leftDest = CalculateLeftDestination();
        var centerDest = CalculateCenterDestination();
        var rightDest = CalculateRightDestination();

        if (center == null)
        {
            var thisMonth = new DateTime(Options.Year, Options.Month, 1);
            var lastMonth = thisMonth.AddMonths(-1);
            var nextMonth = thisMonth.AddMonths(1);
            left = ProtectedPanel.Add(new MonthCalendar(new MonthCalendarOptions() { CustomizeContent = Options.CustomizeContent, MinMonth = Options.MinMonth, MaxMonth = Options.MaxMonth, AdvanceMonthBackwardKey = null, AdvanceMonthForwardKey = null, TodayHighlightColor = Options.TodayHighlightColor, Month = lastMonth.Month, Year = lastMonth.Year }));
            center = ProtectedPanel.Add(new MonthCalendar(new MonthCalendarOptions() { CustomizeContent = Options.CustomizeContent, MinMonth = Options.MinMonth, MaxMonth = Options.MaxMonth, AdvanceMonthBackwardKey = null, AdvanceMonthForwardKey = null, TodayHighlightColor = Options.TodayHighlightColor, Month = thisMonth.Month, Year = thisMonth.Year }));
            right = ProtectedPanel.Add(new MonthCalendar(new MonthCalendarOptions() { CustomizeContent = Options.CustomizeContent, MinMonth = Options.MinMonth, MaxMonth = Options.MaxMonth, AdvanceMonthBackwardKey = null, AdvanceMonthForwardKey = null, TodayHighlightColor = Options.TodayHighlightColor, Month = nextMonth.Month, Year = nextMonth.Year }));

        }

        left.Bounds = leftDest;
        center.Bounds = centerDest;
        right.Bounds = rightDest;
    }


    private void SetupInvisiblePlaceholders()
    {
        var placeholderGrid = ProtectedPanel.Add(new GridLayout("1%;15%;50%;15%;1%", "01%;25%;01%;46%;01%;25%;01%")).Fill();

        leftPlaceHolder = placeholderGrid.Add(new ConsolePanel() { Background = RGB.Green }, 1, 2, 1, 1);
        centerPlaceHolder = placeholderGrid.Add(new ConsolePanel() { Background = RGB.Green }, 3, 1, 1, 3);
        rightPlaceHolder = placeholderGrid.Add(new ConsolePanel() { Background = RGB.Green }, 5, 2, 1, 1);
        placeholderGrid.IsVisible = false;
    }

    private RectF CalculateLeftDestination() => MapPlaceholderBoundsToControlBounds(leftPlaceHolder);
    private RectF CalculateCenterDestination() => MapPlaceholderBoundsToControlBounds(centerPlaceHolder);
    private RectF CalculateRightDestination() => MapPlaceholderBoundsToControlBounds(rightPlaceHolder);

    private RectF MapPlaceholderBoundsToControlBounds(ConsoleControl placeholder) => new RectF(
            placeholder.AbsoluteX - ProtectedPanel.AbsoluteX,
            placeholder.AbsoluteY - ProtectedPanel.AbsoluteY,
            placeholder.Width,
            placeholder.Height);
}
