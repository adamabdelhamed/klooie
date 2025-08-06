using System;

namespace klooie;

/// <summary>
/// Navigation for the Composer grid. Arrow keys move the playhead (when stopped) or pan the viewport (when playing).
/// Vertical navigation pans tracks. Home/End jump to song start/end.
/// </summary>
public class SongComposerNavigationMode : SongComposerInputMode
{
    public override void HandleKeyInput(ConsoleKeyInfo key)
    {
        if (key.Modifiers != 0) return; // ignore any modifiers

        var player = Composer.Player;
        var view = Composer.Viewport;
        bool handled = true;

        var k = key.Key;

        if (k == ConsoleKey.LeftArrow || k == ConsoleKey.A)
        {
            if (player.IsPlaying)
            {
                view.ScrollBeats(-Composer.BeatsPerColumn);
            }
            else
            {
                player.SeekBy(-Composer.BeatsPerColumn);
                EnsurePlayheadVisible();
            }
        }
        else if (k == ConsoleKey.RightArrow || k == ConsoleKey.D)
        {
            if (player.IsPlaying)
            {
                view.ScrollBeats(Composer.BeatsPerColumn);
            }
            else
            {
                player.SeekBy(Composer.BeatsPerColumn);
                EnsurePlayheadVisible();
            }
        }
        else if (k == ConsoleKey.UpArrow || k == ConsoleKey.W)
        {
            view.ScrollRows(-1, Composer.Tracks.Count);
        }
        else if (k == ConsoleKey.DownArrow || k == ConsoleKey.S)
        {
            view.ScrollRows(1, Composer.Tracks.Count);
        }
        else if (k == ConsoleKey.PageUp)
        {
            int delta = view.RowsOnScreen >= 8 ? -4 : -1;
            view.ScrollRows(delta, Composer.Tracks.Count);
        }
        else if (k == ConsoleKey.PageDown)
        {
            int delta = view.RowsOnScreen >= 8 ? 4 : 1;
            view.ScrollRows(delta, Composer.Tracks.Count);
        }
        else if (k == ConsoleKey.Home)
        {
            player.Seek(0);
            EnsurePlayheadVisible();
        }
        else if (k == ConsoleKey.End)
        {
            player.Seek(Composer.MaxBeat);
            EnsurePlayheadVisible();
        }
        else if (k == ConsoleKey.Enter && Composer.SelectedMelodies.Count == 1)
        {
            Composer.OpenMelody(Composer.SelectedMelodies[0]);
        }
        else
        {
            handled = false;
        }

        if (handled)
        {
            Composer.RefreshVisibleSet();
        }
    }

    private void EnsurePlayheadVisible()
    {
        var view = Composer.Viewport;
        double beat = Composer.Player.CurrentBeat;

        if (beat < view.FirstVisibleBeat)
        {
            view.SetFirstVisibleBeat(Math.Max(0, beat));
        }
        else if (beat > view.LastVisibleBeat)
        {
            view.SetFirstVisibleBeat(Math.Max(0, beat - view.BeatsOnScreen));
        }
    }
}
