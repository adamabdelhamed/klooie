using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class BeatGridBackground<T> : ProtectedConsolePanel, IObservableObject
{
    private readonly BeatGrid<T> grid;
    private readonly RGB lightColor;
    private readonly RGB darkColor;
    private readonly RGB darkFocusColor;
    private Func<bool> hasFocus;
    public int CurrentOffset { get; set; }

    private static readonly RGB MainBeatColor = new RGB(40, 40, 40);
    private static readonly RGB SubdivColor = new RGB(150, 150, 150);

    // One cache per bar type: background color => ConsoleString
    private static readonly Dictionary<RGB, ConsoleString> MainBeatBars = new();
    private static readonly Dictionary<RGB, ConsoleString> SubdivBars = new();


    public BeatGridBackground(int currentOffset, BeatGrid<T> grid, RGB lightColor, RGB darkColor, RGB darkFocusColor, Func<bool> hasFocus)
    {
        this.grid = grid;
        this.lightColor = lightColor;
        this.darkColor = darkColor;
        this.CurrentOffset = currentOffset;
        this.darkFocusColor = darkFocusColor;
        this.hasFocus = hasFocus;
        CanFocus = false;
        ZIndex = -1; // Always behind everything else
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        int rows = Height / grid.Viewport.RowHeightChars;
        for (int i = 0; i < rows; i++)
        {
            var bgColor = (i + CurrentOffset) % 2 == 0 ? lightColor : hasFocus() ? darkFocusColor : darkColor;
            context.FillRect(bgColor, 0, i * grid.Viewport.RowHeightChars, Width, grid.Viewport.RowHeightChars);
        }
        DrawBeatGrid(context);
    }

    private void DrawBeatGrid(ConsoleBitmap context)
    {
        double firstBeat = grid.Viewport.FirstVisibleBeat;
        double beatsPerCol = grid.BeatsPerColumn;
        int colWidth = grid.Viewport.ColWidthChars;
        int width = grid.Width;

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
                for (int y = 0; y < grid.Height; y++)
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
                    for (int y = 0; y < grid.Height; y++)
                    {
                        var existingPixel = context.GetPixel(x, y);
                        context.DrawString(GetBar(SubdivBars, SubdivColor, existingPixel.BackgroundColor), x, y);
                    }
                }
            }
        }
    }

    public static ConsoleString GetBar(Dictionary<RGB, ConsoleString> cache, RGB fg, RGB bg)
    {
        if (!cache.TryGetValue(bg, out var cs))
        {
            cs = "|".ToConsoleString(fg, bg);
            cache[bg] = cs;
        }
        return cs;
    }

    private static bool IsNearInteger(double val, double epsilon)
    {
        return Math.Abs(val - Math.Round(val)) < epsilon;
    }
}
