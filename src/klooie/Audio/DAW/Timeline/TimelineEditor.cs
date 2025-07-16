using System;
using System.Collections.Generic;
using System.Linq;

namespace klooie;

public class TimelineEditor
{
    public required VirtualTimelineGrid Timeline { get; init; }
    private readonly List<NoteExpression> clipboard = new();

    public bool HandleKeyInput(ConsoleKeyInfo k)
    {
        if(k.Key == ConsoleKey.A && k.Modifiers == ConsoleModifiers.Control)
        {
            SelectAll();
            return true;
        }
        if(k.Key == ConsoleKey.D && k.Modifiers == ConsoleModifiers.Control)
        {
            DeselectAll();
            return true;
        }
        if(k.Key == ConsoleKey.Delete)
        {
            DeleteSelected();
            return true;
        }
        if(k.Key == ConsoleKey.C && k.Modifiers == ConsoleModifiers.Control)
        {
            Copy();
            return true;
        }
        if(k.Key == ConsoleKey.V && k.Modifiers == ConsoleModifiers.Control)
        {
            Paste();
            return true;
        }
        if(k.Modifiers == ConsoleModifiers.Control && IsArrowKey(k))
        {
            MoveSelection(k);
            return true;
        }
        if(k.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift) &&
            (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.DownArrow))
        {
            AdjustVelocity(k.Key == ConsoleKey.UpArrow ? 1 : -1);
            return true;
        }
        return false;
    }

    private void SelectAll()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(Timeline.NoteSource);
        Timeline.RefreshVisibleSet();
    }

    private void DeselectAll()
    {
        Timeline.SelectedNotes.Clear();
        Timeline.RefreshVisibleSet();
    }

    private void DeleteSelected()
    {
        if(Timeline.NoteSource is not ListNoteSource list) return;
        foreach(var note in Timeline.SelectedNotes)
        {
            list.Remove(note);
        }
        Timeline.SelectedNotes.Clear();
        Timeline.RefreshVisibleSet();
    }

    private void Copy()
    {
        clipboard.Clear();
        clipboard.AddRange(Timeline.SelectedNotes);
    }

    private void Paste()
    {
        if(Timeline.NoteSource is not ListNoteSource list) return;
        if(clipboard.Count == 0) return;
        double offset = Timeline.CurrentBeat - clipboard.Min(n => n.StartBeat);
        var pasted = new List<NoteExpression>();
        foreach(var n in clipboard)
        {
            var nn = NoteExpression.Create(
                n.MidiNote,
                Math.Max(0, n.StartBeat + offset),
                n.DurationBeats,
                n.Velocity,
                n.Instrument);
            list.Add(nn);
            pasted.Add(nn);
        }
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(pasted);
        Timeline.RefreshVisibleSet();
    }

    private void MoveSelection(ConsoleKeyInfo k)
    {
        if(Timeline.NoteSource is not ListNoteSource list) return;
        double beatDelta = 0;
        int midiDelta = 0;
        if(k.Key == ConsoleKey.LeftArrow) beatDelta = -Timeline.BeatsPerColumn;
        else if(k.Key == ConsoleKey.RightArrow) beatDelta = Timeline.BeatsPerColumn;
        else if(k.Key == ConsoleKey.UpArrow) midiDelta = 1;
        else if(k.Key == ConsoleKey.DownArrow) midiDelta = -1;
        var updated = new List<NoteExpression>();
        foreach(var n in Timeline.SelectedNotes.ToArray())
        {
            int idx = list.IndexOf(n);
            if(idx < 0) continue;
            int newMidi = Math.Clamp(n.MidiNote + midiDelta, 0, 127);
            double newBeat = Math.Max(0, n.StartBeat + beatDelta);
            var nn = NoteExpression.Create(newMidi, newBeat, n.DurationBeats, n.Velocity, n.Instrument);
            list[idx] = nn;
            updated.Add(nn);
        }
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(updated);
        Timeline.RefreshVisibleSet();
    }

    private void AdjustVelocity(int delta)
    {
        if(Timeline.NoteSource is not ListNoteSource list) return;
        var updated = new List<NoteExpression>();
        foreach(var n in Timeline.SelectedNotes.ToArray())
        {
            int idx = list.IndexOf(n);
            if(idx < 0) continue;
            int newVel = Math.Clamp(n.Velocity + delta, 1, 127);
            var nn = NoteExpression.Create(n.MidiNote, n.StartBeat, n.DurationBeats, newVel, n.Instrument);
            list[idx] = nn;
            updated.Add(nn);
        }
        Timeline.SelectedNotes.Clear();
        Timeline.SelectedNotes.AddRange(updated);
        Timeline.RefreshVisibleSet();
    }

    private static bool IsArrowKey(ConsoleKeyInfo k) =>
        k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.RightArrow ||
        k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.DownArrow;
}
