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
    protected override List<MelodyClip> GetAllValues() => Grid.Tracks.SelectMany(t => t.Melodies).ToList();
    protected override void RefreshVisibleCells() => Grid.RefreshVisibleCells();
    protected override void FireStatusChanged(ConsoleString msg) => Grid.StatusChanged.Fire(msg);

    protected override bool SelectAllLeftOrRight(ConsoleKeyInfo k) => false;

    protected override IEnumerable<MelodyClip> DeepCopyClipboard(IEnumerable<MelodyClip> src)
        => src.Select(m => new MelodyClip(m.StartBeat, new ListNoteSource(m.Melody)) { Name = m.Name }).ToList();

    protected override bool PasteClipboard()
    {
        if (Clipboard.Count == 0) return true;
        int targetTrack = Composer.TrackHeaders.SelectedTrackIndex;
        if (targetTrack < 0 || targetTrack >= Grid.Tracks.Count) targetTrack = 0;

        double pasteBeat = Grid.Player.CurrentBeat;
        double offset = Clipboard.Min(c => c.StartBeat);

        var pasted = new List<MelodyClip>();
        var addCmds = new List<ICommand>();
        foreach (var clip in Clipboard)
        {
            var newClip = new MelodyClip(Math.Max(0, clip.StartBeat - offset + pasteBeat), clip.Melody)
            {
                Name = clip.Name
            };
            addCmds.Add(new AddMelodyClipCommand(Grid, targetTrack, newClip));
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
                if (Grid.Tracks[trackIdx].Melodies.Contains(melody))
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
            int trackIdx = Grid.Tracks.FindIndex(t => t.Melodies.Contains(clip));
            if (trackIdx < 0) continue;

            var newClip = new MelodyClip(Math.Max(0, clip.StartBeat + beatDelta), clip.Melody) { Name = clip.Name };
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
