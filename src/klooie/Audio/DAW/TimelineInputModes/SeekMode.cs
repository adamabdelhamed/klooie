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
        if (k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A) Player.SeekBy(-Timeline.BeatsPerColumn);
        else if (k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D) Player.SeekBy(Timeline.BeatsPerColumn);
        else if (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W) Player.SeekBy(-0.25 * Timeline.BeatsPerColumn); // Fine-tune
        else if (k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S) Player.SeekBy(+0.25 * Timeline.BeatsPerColumn); // Fine-tune
        else if (k.Key == ConsoleKey.Home) Player.Seek(0); // Go to start
        else if (k.Key == ConsoleKey.End) Player.Seek(Timeline.NoteSource.Select(n => n.StartBeat + n.DurationBeats).LastOrDefault()); // Go to end

        else return;
        Timeline.RefreshVisibleSet();
    }
}