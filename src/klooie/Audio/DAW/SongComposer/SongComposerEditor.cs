using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class SongComposerEditor
{
    public required SongComposer Composer { get; init; }

    // The clipboard stores MelodyClip references (deep copy logic below)
    private readonly List<MelodyClip> clipboard = new();

    private ConsoleControl? addClipPreview;
    private (double Start, double Duration, int TrackIndex, ListNoteSource Melody)? pendingAddClip;

    private CommandStack CommandStack { get; init; }

    public SongComposerEditor(CommandStack commandStack)
    {
        this.CommandStack = commandStack;
    }

    public bool HandleKeyInput(ConsoleKeyInfo k)
    {
        bool Matches(ConsoleKey key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            return k.Key == key
                && (!ctrl || k.Modifiers.HasFlag(ConsoleModifiers.Control))
                && (!shift || k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                && (!alt || k.Modifiers.HasFlag(ConsoleModifiers.Alt))
                && (ctrl ? k.Modifiers.HasFlag(ConsoleModifiers.Control) : !k.Modifiers.HasFlag(ConsoleModifiers.Control))
                && (shift ? k.Modifiers.HasFlag(ConsoleModifiers.Shift) : !k.Modifiers.HasFlag(ConsoleModifiers.Shift))
                && (alt ? k.Modifiers.HasFlag(ConsoleModifiers.Alt) : !k.Modifiers.HasFlag(ConsoleModifiers.Alt));
        }

        // SELECTION
        if (Matches(ConsoleKey.A, ctrl: true)) return SelectAll();
        if (Matches(ConsoleKey.D, ctrl: true)) return DeselectAll();

        // CLIPBOARD
        if (Matches(ConsoleKey.C, shift: true)) return Copy();
        if (Matches(ConsoleKey.V, shift: true)) return Paste();

        // DELETE
        if (Matches(ConsoleKey.Delete)) return DeleteSelected();

        // MOVE
        if (Matches(ConsoleKey.LeftArrow, alt: true) || Matches(ConsoleKey.RightArrow, alt: true)
         || Matches(ConsoleKey.UpArrow, alt: true) || Matches(ConsoleKey.DownArrow, alt: true))
            return MoveSelection(k);

        // DUPLICATE
        if (Matches(ConsoleKey.D, shift: true)) return DuplicateSelected();

        // UNDO/REDO
        if (Matches(ConsoleKey.Z, ctrl: true)) return Undo();
        if (Matches(ConsoleKey.Y, ctrl: true)) return Redo();

        // ADD CLIP PREVIEW
        if (Matches(ConsoleKey.P) && pendingAddClip != null) return CommitAddClip();
        if (Matches(ConsoleKey.D, alt: true) && pendingAddClip != null) return DismissAddClipPreview();

        return false;
    }

    // --- SELECTION ---
    private bool SelectAll()
    {
        Composer.SelectedMelodies.Clear();
        foreach (var track in Composer.Tracks)
            Composer.SelectedMelodies.AddRange(track.Melodies);
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("All melodies selected".ToWhite());
        return true;
    }
    private bool DeselectAll()
    {
        Composer.SelectedMelodies.Clear();
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("Deselected all melodies".ToWhite());
        return true;
    }

    // --- CLIPBOARD ---
    private bool Copy()
    {
        clipboard.Clear();
        Composer.StatusChanged.Fire($"Copied {Composer.SelectedMelodies.Count} melody clips to clipboard".ToWhite());
        // Deep copy: Store all data needed to reconstruct the clips, except track assignment (handled in Paste)
        clipboard.AddRange(Composer.SelectedMelodies
            .Select(m => new MelodyClip(m.StartBeat, new ListNoteSource(m.Melody)) { Name = m.Name }));
        return true;
    }
    private bool Paste()
    {
        if (clipboard.Count == 0) return true;
        // Paste into current track at current playhead, offset by the first pasted clip's start
        int targetTrack = Composer.SelectedTrackIndex;
        if (targetTrack < 0 || targetTrack >= Composer.Tracks.Count) targetTrack = 0;

        double pasteBeat = Composer.CurrentBeat;
        double offset = pasteBeat - clipboard.Min(c => c.StartBeat);

        var pasted = new List<MelodyClip>();
        foreach (var clip in clipboard)
        {
            // Offset to the new paste location
            var newClip = new MelodyClip(Math.Max(0, clip.StartBeat + offset), clip.Melody)
            {
                Name = clip.Name
            };
            Composer.Tracks[targetTrack].Melodies.Add(newClip);
            pasted.Add(newClip);
        }

        Composer.SelectedMelodies.Clear();
        Composer.SelectedMelodies.AddRange(pasted);
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire($"Pasted {pasted.Count} melody clips".ToWhite());
        return true;
    }

    // --- DELETE ---
    private bool DeleteSelected()
    {
        if (Composer.SelectedMelodies.Count == 0) return true;

        foreach (var melody in Composer.SelectedMelodies)
        {
            foreach (var track in Composer.Tracks)
                track.Melodies.Remove(melody);
        }
        Composer.SelectedMelodies.Clear();
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire("Deleted selected melodies".ToWhite());
        return true;
    }

    // --- MOVE ---
    private bool MoveSelection(ConsoleKeyInfo k)
    {
        if (Composer.SelectedMelodies.Count == 0) return true;

        double beatDelta = 0;
        int trackDelta = 0;
        if (k.Key == ConsoleKey.LeftArrow) beatDelta = -Composer.BeatsPerColumn;
        else if (k.Key == ConsoleKey.RightArrow) beatDelta = Composer.BeatsPerColumn;
        else if (k.Key == ConsoleKey.UpArrow) trackDelta = -1;
        else if (k.Key == ConsoleKey.DownArrow) trackDelta = 1;

        var moved = new List<MelodyClip>();
        foreach (var clip in Composer.SelectedMelodies.ToList())
        {
            // Remove from old track if moving vertically
            int trackIdx = Composer.Tracks.FindIndex(t => t.Melodies.Contains(clip));
            int newTrackIdx = Math.Clamp(trackIdx + trackDelta, 0, Composer.Tracks.Count - 1);

            if (trackDelta != 0 && trackIdx != newTrackIdx)
            {
                Composer.Tracks[trackIdx].Melodies.Remove(clip);
                Composer.Tracks[newTrackIdx].Melodies.Add(clip);
            }

            clip.StartBeat = Math.Max(0, clip.StartBeat + beatDelta);
            moved.Add(clip);
        }

        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire($"Moved {moved.Count} melody clips".ToWhite());
        return true;
    }

    // --- DUPLICATE ---
    private bool DuplicateSelected()
    {
        if (Composer.SelectedMelodies.Count == 0) return true;

        int targetTrack = Composer.SelectedTrackIndex;
        if (targetTrack < 0 || targetTrack >= Composer.Tracks.Count) targetTrack = 0;

        var duplicates = new List<MelodyClip>();
        foreach (var clip in Composer.SelectedMelodies)
        {
            var dup = new MelodyClip(clip.StartBeat + clip.DurationBeats, new ListNoteSource(clip.Melody))
            {
                Name = clip.Name + " Copy"
            };
            Composer.Tracks[targetTrack].Melodies.Add(dup);
            duplicates.Add(dup);
        }
        Composer.SelectedMelodies.Clear();
        Composer.SelectedMelodies.AddRange(duplicates);
        Composer.RefreshVisibleSet();
        Composer.StatusChanged.Fire($"Duplicated {duplicates.Count} melody clips".ToWhite());
        return true;
    }

    // --- UNDO/REDO ---
    private bool Undo()
    {
        CommandStack.Undo();
        return true;
    }
    private bool Redo()
    {
        CommandStack.Redo();
        return true;
    }

    // --- ADD CLIP PREVIEW ---
    public void BeginAddClipPreview(double start, double duration, int trackIndex, ListNoteSource melody)
    {
        ClearAddClipPreview();
        pendingAddClip = (start, duration, trackIndex, melody);
        addClipPreview = Composer.AddPreviewControl();
        addClipPreview.Background = RGB.DarkGreen;
        addClipPreview.ZIndex = 0;
        Composer.Viewport.Changed.Subscribe(addClipPreview, _ => PositionAddClipPreview(), addClipPreview);
        PositionAddClipPreview();
        Composer.StatusChanged.Fire(ConsoleString.Parse("[White]Press [Cyan]p[White] to add a melody clip here or press ALT + D to cancel."));
    }

    public void PositionAddClipPreview()
    {
        if (pendingAddClip == null || addClipPreview == null) return;
        var (start, duration, trackIndex, melody) = pendingAddClip.Value;
        int x = ConsoleMath.Round((start - Composer.Viewport.FirstVisibleBeat) / Composer.BeatsPerColumn) * Composer.Viewport.ColWidthChars;
        int y = (trackIndex - Composer.Viewport.FirstVisibleRow) * Composer.Viewport.RowHeightChars;
        int w = Math.Max(1, ConsoleMath.Round(duration / Composer.BeatsPerColumn) * Composer.Viewport.ColWidthChars);
        int h = Composer.Viewport.RowHeightChars;
        addClipPreview.MoveTo(x, y);
        addClipPreview.ResizeTo(w, h);
    }
    public void ClearAddClipPreview()
    {
        addClipPreview?.Dispose();
        addClipPreview = null;
        pendingAddClip = null;
    }
    private bool CommitAddClip()
    {
        if (pendingAddClip == null) return true;
        var (start, duration, trackIndex, melody) = pendingAddClip.Value;
        if (trackIndex < 0 || trackIndex >= Composer.Tracks.Count) return true;

        var newClip = new MelodyClip(start, melody);
        Composer.Tracks[trackIndex].Melodies.Add(newClip);
        Composer.SelectedMelodies.Clear();
        Composer.SelectedMelodies.Add(newClip);
        Composer.RefreshVisibleSet();
        ClearAddClipPreview();
        Composer.StatusChanged.Fire($"Added melody clip \"{newClip.Name}\"".ToWhite());
        return true;
    }
    private bool DismissAddClipPreview()
    {
        ClearAddClipPreview();
        Composer.RefreshVisibleSet();
        return true;
    }
}
