using System;
using System.Collections.Generic;

namespace klooie;

public abstract class MelodyComposerInputMode : IComparable<MelodyComposerInputMode>
{
    public required MelodyComposer Composer { get; init; }

    // Pre-created foreground colors
    private static readonly RGB MainBeatColor = new RGB(40, 40, 40);
    private static readonly RGB SubdivColor = new RGB(150, 150, 150);
    private static readonly RGB PlayHeadGreenColor = RGB.Green;
    private static readonly RGB PlayHeadGrayColor = RGB.Gray;
    private static readonly RGB PlayHeadDarkRedColor = RGB.DarkRed;
    private static readonly RGB PlayHeadRedColor = RGB.Red;

    // One cache per bar type: background color => ConsoleString
    private static readonly Dictionary<RGB, ConsoleString> MainBeatBars = new();
    private static readonly Dictionary<RGB, ConsoleString> SubdivBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadGreenBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadGrayBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadDarkRedBars = new();
    private static readonly Dictionary<RGB, ConsoleString> PlayHeadRedBars = new();

    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    public virtual void Enter() { }

    public virtual void Paint(ConsoleBitmap context)
    {
        DrawBeatGrid(context);
        DrawPlayHead(context);
    }

    // Helper for bar cache lookup/creation
    private static ConsoleString GetBar(Dictionary<RGB, ConsoleString> cache, RGB fg, RGB bg)
    {
        if (!cache.TryGetValue(bg, out var cs))
        {
            cs = "|".ToConsoleString(fg, bg);
            cache[bg] = cs;
        }
        return cs;
    }

    private void DrawPlayHead(ConsoleBitmap context)
    {
        // Decide which cache/fg to use
        Dictionary<RGB, ConsoleString> barCache;
        RGB fg;
        if (Composer.Player.IsPlaying)
        {
            barCache = PlayHeadGreenBars;
            fg = PlayHeadGreenColor;
        }
        else if (Composer.CurrentMode is MelodyComposerSelectionMode)
        {
            barCache = PlayHeadGrayBars;
            fg = PlayHeadGrayColor;
        }
        else if (Composer.CurrentMode is MelodyComposerNavigationMode)
        {
            barCache = PlayHeadDarkRedBars;
            fg = PlayHeadDarkRedColor;
        }
        else
        {
            barCache = PlayHeadRedBars;
            fg = PlayHeadRedColor;
        }

        double relBeat = Composer.Player.CurrentBeat - Composer.Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(relBeat / Composer.BeatsPerColumn) * MelodyComposer.ColWidthChars;
        if (x < 0 || x >= Composer.Width) return;
        for (int y = 0; y < Composer.Height; y++)
        {
            var existingPixel = context.GetPixel(x, y);
            context.DrawString(GetBar(barCache, fg, existingPixel.BackgroundColor), x, y);
        }
    }

    private void DrawBeatGrid(ConsoleBitmap context)
    {
        double firstBeat = Composer.Viewport.FirstVisibleBeat;
        double beatsPerCol = Composer.BeatsPerColumn;
        int colWidth = MelodyComposer.ColWidthChars;
        int width = Composer.Width;

        // Subdivision logic (finest with ≥4 cells apart)
        Span<double> subdivs = stackalloc double[] { 8, 4, 2 };
        double chosenSubdiv = 1;
        foreach (var subdiv in subdivs)
        {
            double cellsBetween = 1.0 / (beatsPerCol * subdiv);
            if (cellsBetween >= 4)
            {
                chosenSubdiv = subdiv;
                break;
            }
        }

        for (int x = 0; x < width; x++)
        {
            double beatAtX = firstBeat + (x / (double)colWidth) * beatsPerCol;

            if (IsNearInteger(beatAtX, 1e-6))
            {
                for (int y = 0; y < Composer.Height; y++)
                {
                    var existingPixel = context.GetPixel(x, y);
                    context.DrawString(GetBar(MainBeatBars, MainBeatColor, existingPixel.BackgroundColor), x, y);
                }
                continue;
            }

            if (chosenSubdiv > 1)
            {
                double beatInSubdivision = beatAtX * chosenSubdiv;
                if (IsNearInteger(beatInSubdivision, 1e-6))
                {
                    for (int y = 0; y < Composer.Height; y++)
                    {
                        var existingPixel = context.GetPixel(x, y);
                        context.DrawString(GetBar(SubdivBars, SubdivColor, existingPixel.BackgroundColor), x, y);
                    }
                }
            }
        }
    }

    // Helper to check if double is near an integer
    private static bool IsNearInteger(double val, double epsilon)
    {
        return Math.Abs(val - Math.Round(val)) < epsilon;
    }

    public int CompareTo(MelodyComposerInputMode? other)
    {
        return this.GetType().FullName == other?.GetType().FullName ? 0 : -1;
    }
}
