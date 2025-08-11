using System;
using System.Collections.Generic;

namespace klooie;

/// <summary>
/// UI-agnostic live MIDI note engine. Starts/ends sustained notes and tracks them by MIDI note number (single channel).
/// The caller supplies a factory that decides how to construct NoteExpression (start beat, instrument, etc).
/// </summary>
public sealed class MidiLiveNoteEngine : Recyclable
{
    private readonly Dictionary<int, SustainedNoteTracker> trackers = new();
    private readonly Dictionary<int, NoteExpression> liveNotes = new();

    private Func<int, int, NoteExpression> noteFactory;

    private static readonly LazyPool<MidiLiveNoteEngine> pool = new(() => new MidiLiveNoteEngine());
    private MidiLiveNoteEngine() { }

    public static MidiLiveNoteEngine Create(Func<int, int, NoteExpression> noteFactory)
    {
        var e = pool.Value.Rent();
        e.noteFactory = noteFactory ?? throw new ArgumentNullException(nameof(noteFactory));
        return e;
    }

    /// <summary>Attempts to start a sustained note for the given MIDI note/velocity.</summary>
    public bool TryStart(int noteNumber, int velocity, out NoteExpression note, out SustainedNoteTracker tracker)
    {
        note = null!;
        tracker = null!;
        if (trackers.ContainsKey(noteNumber)) return false;

        var n = noteFactory(noteNumber, velocity);
        var voices = ConsoleApp.Current.Sound.PlaySustainedNote(n);
        if (voices == null) return false;

        var t = SustainedNoteTracker.Create(n, voices);
        trackers[noteNumber] = t;
        liveNotes[noteNumber] = n;

        note = n;
        tracker = t;
        return true;
    }

    /// <summary>
    /// Stops tracking a sustained note. Returns the original NoteExpression and tracker.
    /// Caller is responsible for calling tracker.ReleaseNote() when appropriate.
    /// </summary>
    public bool TryStop(int noteNumber, out NoteExpression note, out SustainedNoteTracker tracker)
    {
        note = null!;
        tracker = null!;
        if (!trackers.TryGetValue(noteNumber, out var t)) return false;
        if (!liveNotes.TryGetValue(noteNumber, out var n)) return false;

        trackers.Remove(noteNumber);
        liveNotes.Remove(noteNumber);
        note = n;
        tracker = t;
        return true;
    }

    /// <summary>Releases any hanging notes immediately.</summary>
    public void ReleaseAll()
    {
        foreach (var t in trackers.Values)
        {
            try { t.ReleaseNote(); } catch { /* swallow */ }
        }
        trackers.Clear();
        liveNotes.Clear();
    }

    protected override void OnReturn()
    {
        ReleaseAll();
        noteFactory = null!;
        base.OnReturn();
    }
}
