namespace ScrollSucker;
public class HPUpdate : Label
{
    public float Percentage { get; private set; }
    public float CurrentHP { get; private set; }
    public ConsoleControl Target { get; set; }

    private DateTime removeTime;
    public HPUpdate(float current, float max, ConsoleControl target, float delta)
    {
        if (float.IsNaN(current) || float.IsNaN(max)) throw new ArgumentException("NaN not supported for HP Update");
        if (target.IsExpired)
        {
            Dispose();
            return;
        }

        if (target is Character && (target as Character).IsVisible == false)
        {
            Dispose();
            return;
        }

        if (float.IsPositiveInfinity(current))
        {
            Dispose();
            return;
        }

        this.Target = target;
        Refresh(current, max);
        this.Width = 10;
        this.Height = 1;
        this.MoveTo(target.Left, target.Top - 2, 1000);


        target.Sync(nameof(target.Bounds), () =>
        {
            this.MoveTo(target.Left, target.Top - 2, 1000);
        }, EarliestOf(this, target));

        target.OnDisposed(() => TryDispose());

        target.Subscribe(nameof(target.IsVisible), () =>
        {
            if (target.IsVisible == false) TryDispose();
        }, this);

        Game.Current.Invoke(async () =>
        {
            while (ShouldContinue)
            {
                await Task.Yield();
                if (removeTime < DateTime.UtcNow)
                {
                    Dispose();
                }
            }
        });
    }

    public void Refresh(float current, float max)
    {
        if (float.IsNaN(current) || float.IsNaN(max)) throw new ArgumentException("NaN not supported for HP Update");
        CurrentHP = current;
        removeTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(1));
        Percentage = float.IsPositiveInfinity(current) ? 1 : current / max;
        FirePropertyChanged(nameof(Bounds));
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var percentage = Percentage;
        context.Fill(RGB.DarkGray);
        var fill = ConsoleMath.Round(percentage * Width);

        var hp = (int)Math.Ceiling(CurrentHP);
        var hpString = hp.ToString("N0");

        var hpStringLeft = (Width - hpString.Length) / 2;


        if (fill == 0 && Percentage > 0)
        {
            fill = 1;
        }
        else if (fill == Bounds.Width && Percentage < 1)
        {
            // todo - this is not working. The rect is still being filled for some reason.
            fill--;
        }
        var textColor = percentage > .5f ? RGB.Black : RGB.DarkRed;
        var fillColor = percentage > .5f ? RGB.Green : RGB.Red;
        var buffer = new ConsoleCharacter[hpString.Length];
        for (var i = 0; i < hpString.Length; i++)
        {
            var left = hpStringLeft + i;
            buffer[i] = new ConsoleCharacter(hpString[i], textColor, left < fill ? fillColor : RGB.DarkGray);
        }

        context.FillRect(fillColor, 0, 0, fill, (int)Height);
        context.DrawString(new ConsoleString(buffer), hpStringLeft, 0);
    }
}