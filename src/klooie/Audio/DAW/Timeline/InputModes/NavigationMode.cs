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

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
            case ConsoleKey.A:
                if (player.IsPlaying)
                {
                    view.ScrollBeats(-Timeline.BeatsPerColumn);
                }
                else
                {
                    player.SeekBy(-Timeline.BeatsPerColumn);
                    EnsurePlayheadVisible();
                }
                break;
            case ConsoleKey.RightArrow:
            case ConsoleKey.D:
                if (player.IsPlaying)
                {
                    view.ScrollBeats(Timeline.BeatsPerColumn);
                }
                else
                {
                    player.SeekBy(Timeline.BeatsPerColumn);
                    EnsurePlayheadVisible();
                }
                break;
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                view.ScrollRows(1);
                break;
            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                view.ScrollRows(-1);
                break;
            case ConsoleKey.PageUp:
                view.ScrollRows(view.MidisOnScreen >= 24 ? 12 : 1);
                break;
            case ConsoleKey.PageDown:
                view.ScrollRows(view.MidisOnScreen >= 24 ? -12 : -1);
                break;
            case ConsoleKey.Home:
                player.Seek(0);
                EnsurePlayheadVisible();
                break;
            case ConsoleKey.End:
                player.Seek(Timeline.MaxBeat);
                EnsurePlayheadVisible();
                break;
            default:
                handled = false;
                break;
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
