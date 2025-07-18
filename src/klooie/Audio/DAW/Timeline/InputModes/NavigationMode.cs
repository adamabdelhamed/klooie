using System;

namespace klooie;

/// <summary>
/// Combines seeking and panning logic into a single navigation mode.
/// Arrow keys move the playhead when stopped or pan the viewport when playing.
/// Vertical navigation always pans without moving the playhead.
/// Home/End jump to start or end and ensure visibility.
/// </summary>
public class NavigationMode : TimelineInputMode
{
    public override void HandleKeyInput(ConsoleKeyInfo key)
    {
        if (key.Modifiers != 0) return; // ignore any modifiers

        var player = Timeline.Player;
        var view = Timeline.Viewport;
        bool handled = true;

        var k = key.Key;

        if (k == ConsoleKey.LeftArrow || k == ConsoleKey.A)
        {
            if (player.IsPlaying)
            {
                view.ScrollBeats(-Timeline.BeatsPerColumn);
            }
            else
            {
                player.SeekBy(-Timeline.BeatsPerColumn);
                EnsurePlayheadVisible();
            }
        }
        else if (k == ConsoleKey.RightArrow || k == ConsoleKey.D)
        {
            if (player.IsPlaying)
            {
                view.ScrollBeats(Timeline.BeatsPerColumn);
            }
            else
            {
                player.SeekBy(Timeline.BeatsPerColumn);
                EnsurePlayheadVisible();
            }
        }
        else if (k == ConsoleKey.UpArrow || k == ConsoleKey.W)
        {
            view.ScrollRows(1);
        }
        else if (k == ConsoleKey.DownArrow || k == ConsoleKey.S)
        {
            view.ScrollRows(-1);
        }
        else if (k == ConsoleKey.PageUp)
        {
            view.ScrollRows(view.MidisOnScreen >= 24 ? 12 : 1);
        }
        else if (k == ConsoleKey.PageDown)
        {
            view.ScrollRows(view.MidisOnScreen >= 24 ? -12 : -1);
        }
        else if (k == ConsoleKey.Home)
        {
            player.Seek(0);
            EnsurePlayheadVisible();
        }
        else if (k == ConsoleKey.End)
        {
            player.Seek(Timeline.MaxBeat);
            EnsurePlayheadVisible();
        }
        else
        {
            handled = false;
        }

        if (handled)
        {
            Timeline.RefreshVisibleSet();
        }
    }

    private void EnsurePlayheadVisible()
    {
        var view = Timeline.Viewport;
        double beat = Timeline.Player.CurrentBeat;

        if (beat < view.FirstVisibleBeat)
        {
            view.FirstVisibleBeat = Math.Max(0, beat);
        }
        else if (beat > view.LastVisibleBeat)
        {
            view.FirstVisibleBeat = Math.Max(0, beat - view.BeatsOnScreen);
        }
    }
}
