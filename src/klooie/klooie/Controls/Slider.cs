namespace klooie;
public class Slider : ConsoleControl
{
    public RGB BarColor { get => Get<RGB>(); set => Set(value); }
    public RGB HandleColor { get => Get<RGB>(); set => Set(value); }

    public float Min { get => Get<float>(); set => Set(value); }
    public float Max { get => Get<float>(); set => Set(value); }
    public float Value { get => Get<float>(); set => Set(value); }
    public float Increment { get; set; } = 1;
    public bool EnableWAndSKeysForUpDown { get; set; }
    public Slider()
    {
        BarColor = RGB.Gray;
        HandleColor = RGB.White;
        Min = 0;
        Max = 100;
        Width = 10;
        Height = 1;
        ILifetime focusLt = null;

        this.Ready.SubscribeOnce(() =>
        {
            this.Subscribe(AnyProperty, () =>
            {
                if (Min > Max) throw new InvalidOperationException("Max must be >= Min");
                if (Value > Max) throw new InvalidOperationException("Value must be <= Max");
                if (Value < Min) throw new InvalidOperationException("Value must be >= Min");

            }, this);

            this.Focused.Subscribe(() =>
            {
                focusLt?.Dispose();
                focusLt = new Lifetime();
                Application.PushKeyForLifetime(ConsoleKey.RightArrow, SlideUp, focusLt);
                Application.PushKeyForLifetime(ConsoleKey.LeftArrow, SlideDown, focusLt);
                Application.PushKeyForLifetime(ConsoleKey.UpArrow, SlideUp, focusLt);
                Application.PushKeyForLifetime(ConsoleKey.DownArrow, SlideDown, focusLt);
                if (EnableWAndSKeysForUpDown)
                {
                    Application.PushKeyForLifetime(ConsoleKey.D, SlideUp, focusLt);
                    Application.PushKeyForLifetime(ConsoleKey.A, SlideDown, focusLt);
                }
            }, this);

            this.Unfocused.Subscribe(() => focusLt?.Dispose(), this);
        });
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

public class SliderWithValueLabel : ProtectedConsolePanel
{
    private Slider slider;
    private Label label;

    public float Min
    {
        get => slider.Min;
        set
        {
            slider.Min = value;
            FirePropertyChanged(nameof(Min));
        }
    }

    public float Max
    {
        get => slider.Max;
        set
        {
            slider.Max = value;
            FirePropertyChanged(nameof(Max));
        }
    }

    public float Value
    {
        get => slider.Value;
        set
        {
            slider.Value = value;
            FirePropertyChanged(nameof(Value));
        }
    }


    public float Increment
    {
        get => slider.Increment;
        set
        {
            slider.Increment = value;
            FirePropertyChanged(nameof(Increment));
        }
    }

    public SliderWithValueLabel()
    {
        Height = 1;
        var stack = ProtectedPanel.Add(new StackPanel() { Height = 1, Orientation = Orientation.Horizontal, Margin = 2, AutoSize = StackPanel.AutoSizeMode.Width }).FillVertically();
        slider = stack.Add(new Slider());
        label = stack.Add(new Label());

        Action updateSliderWidth = () => slider.Width = this.Width - (slider.Max.ToString().Length + stack.Margin);
        this.Sync(nameof(Bounds), updateSliderWidth, this);
        this.Sync(nameof(slider.Value), updateSliderWidth, this);
        slider.Sync(nameof(slider.Value), () => label.Text = slider.Value.ToString().ToConsoleString(), this);
        slider.Subscribe(nameof(slider.Value), () => FirePropertyChanged(nameof(Value)), this);
    }
}