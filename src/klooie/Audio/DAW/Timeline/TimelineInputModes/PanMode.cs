using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class PanMode : TimelineInputMode
{

    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Modifiers.HasFlag(ConsoleModifiers.Alt) || k.Modifiers.HasFlag(ConsoleModifiers.Control) || k.Modifiers.HasFlag(ConsoleModifiers.Shift)) return;
        var Viewport = Timeline.Viewport;
        // Arrow keys pan the viewport.
        if (k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A)
        {
            if (Viewport.FirstVisibleBeat == 0)
            {
                Timeline.Player.SeekBy(-Timeline.BeatsPerColumn);
            }
            Viewport.ScrollBeats(-Timeline.BeatsPerColumn);
        }
        else if (k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D) Viewport.ScrollBeats(Timeline.BeatsPerColumn);
        else if (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W) Viewport.ScrollRows(+1);
        else if (k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S) Viewport.ScrollRows(-1);

        else if (k.Key == ConsoleKey.PageUp) Viewport.ScrollRows(12); 
        else if (k.Key == ConsoleKey.PageDown) Viewport.ScrollRows(-12); 
        else if (k.Key == ConsoleKey.Home)
        {
            if (Viewport.FirstVisibleBeat == 0)
            {
                Timeline.Player.Seek(0); // Jump to start of timeline
            }
            Viewport.FirstVisibleBeat = 0; // Jump to start
        }
        else if (k.Key == ConsoleKey.End)
        {
            Viewport.FirstVisibleBeat = Math.Max(0, Timeline.MaxBeat - Viewport.BeatsOnScreen); // Jump to end
        }

        else return;
        Timeline.RefreshVisibleSet();
    }
}
