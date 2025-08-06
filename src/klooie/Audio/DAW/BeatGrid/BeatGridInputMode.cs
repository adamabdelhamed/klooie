using System;
using System.Collections.Generic;

namespace klooie;

public abstract class BeatGridInputMode<T> : IComparable<BeatGridInputMode<T>>
{
    public required BeatGrid<T> Composer { get; init; }

    // Pre-created foreground colors

    private static readonly RGB PlayHeadGreenColor = RGB.Green;
    private static readonly RGB PlayHeadGrayColor = RGB.Gray;
    private static readonly RGB PlayHeadDarkRedColor = RGB.DarkRed;
    private static readonly RGB PlayHeadRedColor = RGB.Red;

    private static readonly Dictionary<RGB, ConsoleString> PlayingBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadGrayBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadDarkRedBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadRedBars = new();

    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    public virtual void Enter() { }

    public virtual void Paint(ConsoleBitmap context)
    {
        DrawPlayHead(context);
    }

    private void DrawPlayHead(ConsoleBitmap context)
    {
        Dictionary<RGB, ConsoleString> barCache;
        RGB fg;
        if (Composer.Player.IsPlaying)
        {
            barCache = PlayingBars;
            fg = PlayHeadGreenColor;
        }
        else if (Composer.IsNavigating)
        {
            barCache = PlayHeadDarkRedBars;
            fg = PlayHeadDarkRedColor;
        }
        else 
        {
            barCache = PlayHeadGrayBars;
            fg = PlayHeadGrayColor;
        }

        double relBeat = Composer.Player.CurrentBeat - Composer.Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(relBeat / Composer.BeatsPerColumn) * Composer.Viewport.ColWidthChars;
        if (x < 0 || x >= Composer.Width) return;
        for (int y = 0; y < Composer.Height; y++)
        {
            var existingPixel = context.GetPixel(x, y);
            context.DrawString(BeatGridBackground<object>.GetBar(barCache, fg, existingPixel.BackgroundColor), x, y);
        }
    }

    public int CompareTo(BeatGridInputMode<T>? other)
    {
        return this.GetType().FullName == other?.GetType().FullName ? 0 : -1;
    }
}
