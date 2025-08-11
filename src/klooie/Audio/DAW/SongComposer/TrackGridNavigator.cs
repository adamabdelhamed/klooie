using System;

namespace klooie;

/// <summary>
/// Navigation for the Composer grid. Arrow keys move the playhead (when stopped) or pan the viewport (when playing).
/// Vertical navigation pans tracks. Home/End jump to song start/end.
/// </summary>
public class TrackGridNavigator : BeatGridInputMode<MelodyClip>
{
    public TrackGrid SongComposer => Composer as TrackGrid
        ?? throw new InvalidOperationException("This mode can only be used with a SongComposer instance.");

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
            view.ScrollRows(-1, SongComposer.Tracks.Count);
        }
        else if (k == ConsoleKey.DownArrow || k == ConsoleKey.S)
        {
            view.ScrollRows(1, SongComposer.Tracks.Count);
        }
        else if (k == ConsoleKey.PageUp)
        {
            int delta = view.RowsOnScreen >= 8 ? -4 : -1;
            view.ScrollRows(delta, SongComposer.Tracks.Count);
        }
        else if (k == ConsoleKey.PageDown)
        {
            int delta = view.RowsOnScreen >= 8 ? 4 : 1;
            view.ScrollRows(delta, SongComposer.Tracks.Count);
        }
        else if (k == ConsoleKey.Home)
        {
            player.Seek(0);
            EnsurePlayheadVisible();
        }
        else if (k == ConsoleKey.End)
        {
            var beatsPerColumn = Composer.BeatsPerColumn;
            var nextColumn = Math.Ceiling(Composer.MaxBeat / beatsPerColumn) * beatsPerColumn;
            player.Seek(nextColumn);
            EnsurePlayheadVisible();
        }
        else if (k == ConsoleKey.Enter && Composer.SelectedValues.Count == 1)
        {
            SongComposer.OpenMelody(Composer.SelectedValues[0]);
        }
        else if(k == ConsoleKey.F2 && Composer.SelectedValues.Count == 1)
        {
            ConsoleApp.Current.Invoke(async () =>
            {
                var newName = await TextInputDialog.Show(new ShowTextInputOptions("Enter a new name for the selected clip".ToYellow())
                {
                    SpeedPercentage = 0,
                    DialogWidth = 50
                });
                if (string.IsNullOrWhiteSpace(newName?.StringValue)) return;
                WorkspaceSession.Current.Commands.Execute(new RenameClipCommand(Composer.SelectedValues[0], newName.StringValue));
            });
        }
        else
        {
            handled = false;
        }

        if (handled)
        {
            Composer.RefreshVisibleCells();
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
