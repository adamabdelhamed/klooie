﻿namespace klooie;
/// <summary>
/// A panel that will have a uniform colored border around it
/// </summary>
public class BorderPanel : ProtectedConsolePanel
{
    private ConsolePanel container;

    /// <summary>
    /// Set this to override the border color. By default the panel will try to find a dark version of your content's background color.
    /// </summary>
    public RGB? BorderColor { get => Get<RGB?>(); set => Set(value); }

    public ConsoleString Adornment { get => Get<ConsoleString>(); set => Set(value); }

    public BorderPanel(ConsoleControl content = null)
    {
        container = ProtectedPanel.Add(new ConsolePanel()).Fill(padding: new Thickness(2, 2, 1, 1));
        if (content != null)
        {
            container.Background = content.Background;
            ProtectedPanel.Background = content.Background;
            container.Add(content);
        }
        this.Subscribe(nameof(Background), () => container.Background = this.Background, this);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        if (BorderColor.HasValue == false)
        {
            BorderColor = container.Background.ToOther(RGB.Black, .5f);
        }
        base.OnPaint(context);
        var pen = new ConsoleCharacter(' ', backgroundColor: BorderColor.Value);
        context.DrawLine(pen, 0, 0, 0, Height);
        context.DrawLine(pen, 1, 0, 1, Height);
        context.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        context.DrawLine(pen, Width - 2, 0, Width - 2, Height);
        context.DrawLine(pen, 0, 0, Width, 0);
        context.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        if (Adornment != null)
        {
            context.DrawString(Adornment, Width - Adornment.Length - 3, Height - 1);
        }
    }
}
