using klooie;
using System;
using System.Collections.Generic;
using System.Linq;

public class TrackGridEditor : BaseGridEditor<TrackGrid, MelodyClip>
{
    private readonly SongComposer Composer;
    protected override TrackGrid Grid => Composer.Grid;
    public TrackGridEditor(SongComposer composer, CommandStack commandStack) : base(commandStack) => Composer = composer;

    protected override List<MelodyClip> GetSelectedValues() => Grid.SelectedValues;
    protected override List<MelodyClip> GetAllValues() => Grid.Tracks.SelectMany(t => t.Clips).ToList();
    protected override void RefreshVisibleCells() => Grid.RefreshVisibleCells();
    protected override void FireStatusChanged(ConsoleString msg) => Grid.StatusChanged.Fire(msg);

    protected override bool SelectAllLeftOrRight(ConsoleKeyInfo k)
    {
        var left = k.Key == ConsoleKey.LeftArrow;
        var sel = GetSelectedValues();
        sel.Clear();
        sel.AddRange(Grid.Tracks.SelectMany(t => t.Clips).Where(n =>
            (left && n.StartBeat <= Grid.Player.CurrentBeat) ||
            (!left && n.StartBeat >= Grid.Player.CurrentBeat)));
        RefreshVisibleCells();

        return true;
    }

    protected override IEnumerable<MelodyClip> DeepCopyClipboard(IEnumerable<MelodyClip> src)
        => src.Select(m => new MelodyClip(m.StartBeat, new ListNoteSource(m.Notes) { BeatsPerMinute = WorkspaceSession.Current.CurrentSong.BeatsPerMinute }) { Name = m.Name }).ToList();

    protected override bool PasteClipboard()
    {
        if (Clipboard.Count == 0) return true;
 
        double pasteBeat = Grid.Player.CurrentBeat;
        double offset = Clipboard.Min(c => c.StartBeat);

        var pasted = new List<MelodyClip>();
        var addCmds = new List<ICommand>();
        foreach (var clip in Clipboard)
        {
            var trackIndex = Grid.Tracks.FindIndex(t => t.Clips.Contains(clip));
            if(trackIndex < 0) throw new InvalidOperationException("Clipboard clip not found in any track");
            var newClip = new MelodyClip(Math.Max(0, clip.StartBeat - offset + pasteBeat), clip.Notes)
            {
                Name = clip.Name
            };
            addCmds.Add(new AddMelodyClipCommand(Grid, trackIndex, newClip));
            pasted.Add(newClip);
        }
        CommandStack.Execute(new MultiCommand(addCmds, "Paste Melody Clips"));

        Grid.SelectedValues.Clear();
        Grid.SelectedValues.AddRange(pasted);
        Grid.RefreshVisibleCells();
        Grid.StatusChanged.Fire($"Pasted {pasted.Count} melody clips".ToWhite());
        return true;
    }

    protected override bool DeleteSelected()
    {
        if (Grid.SelectedValues.Count == 0) return true;

        var deleteCmds = new List<ICommand>();
        foreach (var melody in Grid.SelectedValues)
        {
            for (int trackIdx = 0; trackIdx < Grid.Tracks.Count; ++trackIdx)
            {
                if (Grid.Tracks[trackIdx].Clips.Contains(melody))
                {
                    deleteCmds.Add(new DeleteMelodyClipCommand(Grid, trackIdx, melody));
                    break;
                }
            }
        }
        CommandStack.Execute(new MultiCommand(deleteCmds, "Delete Melody Clips"));
        Grid.SelectedValues.Clear();
        Grid.RefreshVisibleCells();
        Grid.StatusChanged.Fire("Deleted selected melodies".ToWhite());
        return true;
    }

    protected override bool HandleUnhandledKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.X && k.Modifiers.HasFlag(ConsoleModifiers.Shift) && Grid.SelectedValues.Count == 1)
        {
            var clip = Grid.SelectedValues[0];
            var trackIndex = Grid.Tracks.FindIndex(t => t.Clips.Contains(clip));
            if (trackIndex < 0) throw new InvalidOperationException("Selected clip not found in any track");

            var splitPoint = Grid.Player.CurrentBeat;

            // compute absolute end of the clip
            var clipEnd = clip.StartBeat + (clip.Notes.Count == 0 ? 0 : clip.Notes.Max(n => n.StartBeat + n.DurationBeats));
            if (splitPoint <= clip.StartBeat || splitPoint >= clipEnd) return true; // nothing to split

            var offset = splitPoint - clip.StartBeat;

            // Snapshot and CLONE notes (do NOT mutate originals)
            var leftNotes = clip.Notes
                .Where(n => clip.StartBeat + n.StartBeat < splitPoint)
                .Select(n => NoteExpression.Create(n.MidiNote,  n.StartBeat, n.DurationBeats,  n.BeatsPerMinute, n.Velocity, n.Instrument))
                .ToList();

            var rightNotes = clip.Notes
                .Where(n => clip.StartBeat + n.StartBeat >= splitPoint)
                .Select(n => NoteExpression.Create( n.MidiNote, n.StartBeat - offset, n.DurationBeats, n.BeatsPerMinute, n.Velocity, n.Instrument))
                .ToList();

            var newClip1 = new MelodyClip(clip.StartBeat, new ListNoteSource(leftNotes)) { Name = clip.Name + " (split 1)" };
            var newClip2 = new MelodyClip(splitPoint, new ListNoteSource(rightNotes)) { Name = clip.Name + " (split 2)" };

            CommandStack.Execute(new MultiCommand(
            [
                new AddMelodyClipCommand(Grid, trackIndex, newClip1),
                new AddMelodyClipCommand(Grid, trackIndex, newClip2),
                new DeleteMelodyClipCommand(Grid, trackIndex, clip)
            ], "Split Melody Clip"));

            Grid.SelectedValues.Clear();
            return true;
        }
        return base.HandleUnhandledKeyInput(k);
    }


    protected override bool MoveSelection(ConsoleKeyInfo k)
    {
        if (Grid.SelectedValues.Count == 0) return true;

        double beatDelta = 0;
        // No vertical (track) move for now per your request
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Grid.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Grid.BeatsPerColumn;

        var moveCmds = new List<ICommand>();
        var movedClips = new List<MelodyClip>();
        foreach (var clip in Grid.SelectedValues.ToList())
        {
            int trackIdx = Grid.Tracks.FindIndex(t => t.Clips.Contains(clip));
            if (trackIdx < 0) continue;

            var newClip = new MelodyClip(Math.Max(0, clip.StartBeat + beatDelta), clip.Notes) { Name = clip.Name };
            moveCmds.Add(new ChangeMelodyClipCommand(Grid, trackIdx, clip, newClip));
            movedClips.Add(newClip);
        }
        if (moveCmds.Count > 0)
        {
            CommandStack.Execute(new MultiCommand(moveCmds, "Move Melody Clips"));
            Grid.SelectedValues.Clear();
            Grid.SelectedValues.AddRange(movedClips);
            Grid.RefreshVisibleCells();
            Grid.StatusChanged.Fire($"Moved {movedClips.Count} melody clips".ToWhite());
        }
        return true;
    }
}
