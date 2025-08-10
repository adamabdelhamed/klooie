using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace klooie;


public sealed class InstrumentExpression
{
    public string Name { get; set; }
    [JsonIgnore]
    public Func<ISynthPatch> PatchFunc { get; set; }

    public InstrumentExpression() { }

    private InstrumentExpression(string name, Func<ISynthPatch> patchFunc)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Instrument name cannot be null or empty.", nameof(name));
        if (patchFunc == null) throw new ArgumentNullException(nameof(patchFunc), "Patch function cannot be null.");
        Name = name;
        PatchFunc = patchFunc;
    }
    public static InstrumentExpression Create(string name, Func<ISynthPatch> patchFunc) => new(name, patchFunc);
}

public sealed class NoteExpression : IEquatable<NoteExpression>
{
    public int MidiNote { get; set; }

    [JsonIgnore]
    public float FrequencyHz => MidiNoteToFrequency(MidiNote);
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
    public int Velocity { get; set; }
    [JsonIgnore]
    public double BeatsPerMinute { get; set; }

    [JsonIgnore]
    public TimeSpan StartTime => TimeSpan.FromSeconds(StartBeat * (60.0 / BeatsPerMinute));
    [JsonIgnore]
    public TimeSpan DurationTime => TimeSpan.FromSeconds(DurationBeats * (60.0 / BeatsPerMinute));

    [JsonIgnore]
    public double EndBeat => StartBeat < 0 ? -1 : StartBeat + DurationBeats;

    public InstrumentExpression? Instrument { get; set; }

    public NoteExpression() { }

    private NoteExpression(int midiNote, double startBeat, double durationBeats, int velocity, InstrumentExpression? instrument)
    {
        MidiNote = midiNote;
        StartBeat = startBeat;
        DurationBeats = durationBeats;
        Velocity = velocity;
        Instrument = instrument;
    }

    private NoteExpression(int midiNote, double startBeat, double durationBeats, double bpm, int velocity, InstrumentExpression? instrument)
    {
        MidiNote = midiNote;
        StartBeat = startBeat;
        DurationBeats = durationBeats;
        Velocity = velocity;
        Instrument = instrument;
        BeatsPerMinute = bpm;
    }
    public static NoteExpression Create(int midi, double startBeat, double durationBeats, int velocity = 127, InstrumentExpression? instrument = null)
        => new(midi, startBeat, durationBeats, velocity, instrument);

    // TODO: Parameter confusion - Maybe change the name of the method
    public static NoteExpression Create(int midi, double durationBeats, int velocity = 127, InstrumentExpression? instrument = null)
    => new(midi, -1, durationBeats, velocity, instrument);

    public static NoteExpression Create(int midi, double startBeat, double durationBeats, double bpm, int velocity = 127, InstrumentExpression? instrument = null)
        => new(midi, startBeat, durationBeats, bpm, velocity, instrument);

    public static NoteExpression Rest(double beats)
    => new(0, -1, beats, 0, null);

    public static NoteExpression Rest(double startBeat, double beats)
        => new(0, startBeat, beats, 0, null);

    // Map helpers
    public NoteExpression WithInstrument(InstrumentExpression instrument) => new(MidiNote, StartBeat, DurationBeats, Velocity, instrument);
    public NoteExpression WithInstrumentIfNull(InstrumentExpression instrument) => new(MidiNote, StartBeat, DurationBeats, Velocity, this.Instrument ?? instrument);
    public NoteExpression WithOctave(int octaveDelta) => new(MidiNote + octaveDelta * 12, StartBeat, DurationBeats, Velocity, Instrument);
    public NoteExpression WithVelocity(int velocity) => new(MidiNote, StartBeat, DurationBeats, velocity, Instrument);
    public NoteExpression WithDuration(double beats) => new(MidiNote, StartBeat, beats, Velocity, Instrument);
    public NoteExpression WithStartBeat(double startBeat) => new(MidiNote, startBeat, DurationBeats, Velocity, Instrument);

    public static float MidiNoteToFrequency(int noteNumber)
    {
        return 440f * (float)Math.Pow(2, (noteNumber - 69) / 12.0);
    }
    public override string ToString() => $"Note(Midi: {MidiNote}, Start: {StartBeat}, Duration: {DurationBeats}, Velocity: {Velocity})";

    public override bool Equals(object? obj)
    {
        return Equals(obj as NoteExpression);
    }

    public bool Equals(NoteExpression? other)
    {
        if (other is null) return false;
        return MidiNote == other.MidiNote &&
               StartBeat == other.StartBeat &&
               DurationBeats == other.DurationBeats &&
               Velocity == other.Velocity &&
               BeatsPerMinute == other.BeatsPerMinute &&
               Instrument?.Name == other.Instrument?.Name;
    }
}


/// <summary>
/// A mutable list of notes with high-level sequencing and mapping helpers.
/// </summary>
public class ListNoteSource : List<NoteExpression>
{
    [JsonIgnore]
    public double BeatsPerMinute { get; set; } = 60;

    public ListNoteSource() : base() { }

    public ListNoteSource(IEnumerable<NoteExpression> notes)
        : base(NormalizeStartBeats(notes)) { }

    // Normalizes -1 StartBeats (auto-position)
    private static IEnumerable<NoteExpression> NormalizeStartBeats(IEnumerable<NoteExpression> notes)
    {
        var newNotes = new List<NoteExpression>();
        var i = 0;
        foreach (var note in notes)
        {
            if (note.StartBeat < 0 && i == 0)
            {
                newNotes.Add(note.WithStartBeat(0));
            }
            else if (note.StartBeat < 0 && i > 0)
            {
                var prev = newNotes[i - 1];
                newNotes.Add(note.WithStartBeat(prev.StartBeat + prev.DurationBeats));
            }
            else
            {
                newNotes.Add(note);
            }
            i++;
        }
        return newNotes;
    }

    // Sort notes in place by StartTime (for playback or display)
    public void SortMelody()
    {
        this.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
    }

    // Returns the end time (start + duration) of the last note
    public double GetEndBeat()
        => this.Count == 0 ? 0 : this.Max(n => n.StartBeat + n.DurationBeats);

    /// <summary>
    /// Sequentially appends all notes from 'next', shifting their StartBeats after this source's end.
    /// Optionally trims notes from the end before appending.
    /// </summary>
    public void AddSequential(IEnumerable<NoteExpression> next, int toRemove = 0)
    {
        double offset = GetEndBeat();
        while (toRemove > 0 && this.Count > 0)
        {
            offset -= this.Last().DurationBeats;
            this.RemoveAt(this.Count - 1);
            toRemove--;
        }
        foreach (var n in next)
            this.Add(n.WithStartBeat(n.StartBeat + offset));
    }

    /// <summary>
    /// Adds all notes from 'other' in parallel (overlays, doesn't shift start).
    /// </summary>
    public void AddParallel(IEnumerable<NoteExpression> other)
    {
        this.AddRange(other);
    }

    /// <summary>
    /// Appends multiple repeats of the current notes, each shifted by the loop length.
    /// </summary>
    public void RepeatInPlace(int count)
    {
        if (count < 2) return; // nothing to do for 0,1
        var loopLen = GetEndBeat();
        var original = this.ToList();
        for (int i = 1; i < count; i++)
        {
            double offset = i * loopLen;
            this.AddRange(original.Select(n => n.WithStartBeat(n.StartBeat + offset)));
        }
    }

    // ----- In-place mapping helpers -----

    public ListNoteSource SetInstrument(InstrumentExpression instrument)
    {
        for (int i = 0; i < this.Count; i++)
            this[i] = this[i].WithInstrument(instrument);
        return this;
    }

    public ListNoteSource SetInstrumentIfNull(InstrumentExpression instrument)
    {
        for (int i = 0; i < this.Count; i++)
            this[i] = this[i].WithInstrumentIfNull(instrument);
        return this;
    }

    public ListNoteSource ShiftOctave(int octaveDelta)
    {
        for (int i = 0; i < this.Count; i++)
            this[i] = this[i].WithOctave(octaveDelta);
        return this;
    }

    public ListNoteSource SetVelocity(int velocity)
    {
        for (int i = 0; i < this.Count; i++)
            this[i] = this[i].WithVelocity(velocity);
        return this;
    }

    public ListNoteSource SetDuration(double beats)
    {
        for (int i = 0; i < this.Count; i++)
            this[i] = this[i].WithDuration(beats);
        return this;
    }

    // Add a rest of the specified beats at the end
    public ListNoteSource AddRest(double beats)
    {
        this.Add(NoteExpression.Rest(GetEndBeat(), beats));
        return this;
    }


    // Static: create a ListNoteSource with a single rest at the given beat
    public static ListNoteSource Rest(double startBeat, double beats)
        => new(new[] { NoteExpression.Rest(startBeat, beats) });

}


public class Song
{
    public ListNoteSource Notes { get; protected set; }
    public int Count => Notes.Count;
    public double BeatsPerMinute => Notes.BeatsPerMinute;
    public NoteExpression this[int index] => Notes[index];
    public Song(ListNoteSource notes, double bpm = 60)
    {
        Notes = notes;
        Notes.BeatsPerMinute = bpm;
        for(var i = 0; i < Notes.Count; i++)
        {
            Notes[i].BeatsPerMinute=bpm;
        }
        Notes.SortMelody();
    }
}

