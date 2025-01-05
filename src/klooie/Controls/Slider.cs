namespace klooie;
public partial class Slider : ConsoleControl
{
    public partial RGB BarColor { get; set; }
    public partial RGB HandleColor { get; set; }

    public partial float Min { get; set; }
    public partial float Max { get; set; }
    public partial float Value { get; set; }
    public partial float Increment { get; set; }
    public bool EnableWAndSKeysForUpDown { get; set; }
    public Slider()
    {
        BarColor = RGB.Gray;
        HandleColor = RGB.White;
        Min = 0;
        Max = 100;
        Width = 10;
        Height = 1;
        Increment = 1;
        ILifetime focusLt = null;

        this.Ready.SubscribeOnce(() =>
        {
            SubscribeToAnyPropertyChange(this, OnAnyPropertyChanged, this);

            this.Focused.Subscribe(() =>
            {
                focusLt?.Dispose();
                focusLt = new Lifetime();
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.RightArrow, SlideUp, focusLt);
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.LeftArrow, SlideDown, focusLt);
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, SlideUp, focusLt);
                ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, SlideDown, focusLt);
                if (EnableWAndSKeysForUpDown)
                {
                    ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.D, SlideUp, focusLt);
                    ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.A, SlideDown, focusLt);
                }
            }, this);

            this.Unfocused.Subscribe(() => focusLt?.Dispose(), this);
        });
    }
    private static void OnAnyPropertyChanged(object me)
    {
        var _this = (Slider)me;
        _this.OnAnyPropertyChanged();
    }
    private void OnAnyPropertyChanged()
    {
        if (Min > Max) throw new InvalidOperationException("Max must be >= Min");
        if (Value > Max) throw new InvalidOperationException("Value must be <= Max");
        if (Value < Min) throw new InvalidOperationException("Value must be >= Min");
    }

    private void SlideUp()
    {
        var newVal = Math.Min(Max, Value + Increment);
        Value = newVal;
    }

    private void SlideDown()
    {
        var newVal = Math.Max(Min, Value - Increment);
        Value = newVal;
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        context.FillRect(new ConsoleCharacter('-', BarColor, Background), 0, 0, Width, Height);

        var delta = Value - Min;
        var range = Max - Min;
        var percentage = delta / range;
        var left = (int)ConsoleMath.Round(percentage * (Width - 1));

        var barColor = HasFocus ? FocusColor : BarColor;
        context.DrawPoint(new ConsoleCharacter(' ', Background, barColor), left, 0);
    }
}

public partial class SliderWithValueLabel : ProtectedConsolePanel
{
    private Slider slider;
    private Label label;

    public partial float Min { get; set; }
    public partial float Max { get; set; }

    public partial float Value { get; set; }

    public partial float Increment { get; set; }

    public SliderWithValueLabel()
    {
        Height = 1;
        var stack = ProtectedPanel.Add(new StackPanel() { Height = 1, Orientation = Orientation.Horizontal, Margin = 2, AutoSize = StackPanel.AutoSizeMode.Width }).FillVertically();
        slider = stack.Add(new Slider());
        label = stack.Add(new Label());

        Action updateSliderWidth = () => slider.Width = this.Width - (slider.Max.ToString().Length + stack.Margin);
        this.BoundsChanged.Sync(updateSliderWidth, this);
        
        this.ValueChanged.Sync(updateSliderWidth, this);
        slider.ValueChanged.Subscribe(() => label.Text = slider.Value.ToString().ToConsoleString(), this);
        slider.ValueChanged.Subscribe(() => ValueChanged.Fire(), this);

        MinChanged.Subscribe(() => slider.Min = Min, this);
        MaxChanged.Subscribe(() => slider.Max = Max, this);
        ValueChanged.Subscribe(() => slider.Value = Value, this);
        IncrementChanged.Subscribe(() => slider.Increment = Increment, this);

        slider.MinChanged.Subscribe(() => this.Min = slider.Min, this);
        slider.MaxChanged.Subscribe(() => this.Max = slider.Max, this);
        slider.ValueChanged.Subscribe(() => this.Value = slider.Value, this);
        slider.IncrementChanged.Subscribe(() => this.Increment = slider.Increment, this);
    }
}