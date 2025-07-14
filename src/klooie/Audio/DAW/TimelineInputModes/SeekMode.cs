using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class SeekMode : TimelineInputMode
{
    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Modifiers.HasFlag(ConsoleModifiers.Alt) || k.Modifiers.HasFlag(ConsoleModifiers.Control) || k.Modifiers.HasFlag(ConsoleModifiers.Shift)) return;
        var Player = Timeline.Player;
        // Arrow keys move playhead, not the viewport.
        if (k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A) Player.SeekBy(-1);
        else if (k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D) Player.SeekBy(1);
        else if (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W) Player.SeekBy(-0.25); // Fine-tune
        else if (k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S) Player.SeekBy(+0.25); // Fine-tune

        else return;
        Timeline.RefreshVisibleSet();
    }
}