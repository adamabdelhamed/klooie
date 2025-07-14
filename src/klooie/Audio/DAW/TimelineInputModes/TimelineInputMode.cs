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
        bool flowControl = DrawPlayHead(context);
        if (!flowControl)
        {
            return;
        }
    }

    private bool DrawPlayHead(ConsoleBitmap context)
    {
        var playHeadColor = Timeline.Player.IsPlaying ? RGB.Green :
            Timeline.CurrentMode is SelectionMode ? RGB.Gray : Timeline.CurrentMode is PanMode ? RGB.DarkGray : RGB.Red;
        double relBeat = Timeline.CurrentBeat - Timeline.Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(relBeat / Timeline.BeatsPerColumn) * VirtualTimelineGrid.ColWidthChars;
        if (x < 0 || x >= Timeline.Width) return false;
        for (int y = 0; y < Timeline.Height; y++)
        {
            var existingPixel = context.GetPixel(x, y);
            context.DrawString("|".ToConsoleString(playHeadColor, existingPixel.BackgroundColor), x, y);
        }

        return true;
    }

    public int CompareTo(TimelineInputMode? other)
    {
        return this.GetType().FullName == other?.GetType().FullName ? 0 : -1;
    }
}