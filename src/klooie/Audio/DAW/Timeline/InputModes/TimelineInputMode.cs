using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public abstract class TimelineInputMode : IComparable<TimelineInputMode>
{
    public required VirtualTimelineGrid Timeline { get; init; }

    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    public virtual void Enter() { }

    public virtual void Paint(ConsoleBitmap context)
    {
        DrawBeatGrid(context);
        DrawPlayHead(context);
    }

    private void DrawPlayHead(ConsoleBitmap context)
    {
        var playHeadColor = Timeline.Player.IsPlaying ? RGB.Green :
            Timeline.CurrentMode is SelectionMode ? RGB.Gray : Timeline.CurrentMode is NavigationMode ? RGB.DarkRed : RGB.Red;
        double relBeat = Timeline.CurrentBeat - Timeline.Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(relBeat / Timeline.BeatsPerColumn) * VirtualTimelineGrid.ColWidthChars;
        if (x < 0 || x >= Timeline.Width) return;
        for (int y = 0; y < Timeline.Height; y++)
        {
            var existingPixel = context.GetPixel(x, y);
            context.DrawString("|".ToConsoleString(playHeadColor, existingPixel.BackgroundColor), x, y);
        }
    }

    private void DrawBeatGrid(ConsoleBitmap context)
    {
        double firstBeat = Timeline.Viewport.FirstVisibleBeat;
        double beatsPerCol = Timeline.BeatsPerColumn;
        int colWidth = VirtualTimelineGrid.ColWidthChars;
        int width = Timeline.Width;

        // Colors
        var mainColor = new RGB(40, 40, 40);
        var subColor = new RGB(150, 150, 150);

        // Subdivision logic (finest with ≥4 cells apart)
        double[] subdivs = { 8, 4, 2 };
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

        // Loop over columns, compute beat at that column
        for (int x = 0; x < width; x++)
        {
            double beatAtX = firstBeat + (x / (double)colWidth) * beatsPerCol;

            // Check if this is a main beat (within epsilon of an int)
            if (IsNearInteger(beatAtX, 1e-6))
            {
                for (int y = 0; y < Timeline.Height; y++)
                {
                    var existingPixel = context.GetPixel(x, y);
                    context.DrawString("|".ToConsoleString(mainColor, existingPixel.BackgroundColor), x, y);
                }
                continue;
            }

            // Otherwise, check for subdivision (if enabled)
            if (chosenSubdiv > 1)
            {
                double beatInSubdivision = beatAtX * chosenSubdiv;
                if (IsNearInteger(beatInSubdivision, 1e-6))
                {
                    for (int y = 0; y < Timeline.Height; y++)
                    {
                        var existingPixel = context.GetPixel(x, y);
                        context.DrawString("|".ToConsoleString(subColor, existingPixel.BackgroundColor), x, y);
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

    public int CompareTo(TimelineInputMode? other)
    {
        return this.GetType().FullName == other?.GetType().FullName ? 0 : -1;
    }
}