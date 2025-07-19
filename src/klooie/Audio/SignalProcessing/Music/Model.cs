using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace klooie;


public sealed class InstrumentExpression
{
    public string Name { get; }
    public Func<ISynthPatch> PatchFunc { get; }
    private InstrumentExpression(string name, Func<ISynthPatch> patchFunc)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Instrument name cannot be null or empty.", nameof(name));
        if (patchFunc == null) throw new ArgumentNullException(nameof(patchFunc), "Patch function cannot be null.");
        Name = name;
        PatchFunc = patchFunc;
    }
    public static InstrumentExpression Create(string name, Func<ISynthPatch> patchFunc) => new(name, patchFunc);
}

public sealed class NoteExpression
{
    public int MidiNote { get; }

    public float FrequencyHz => MidiNoteToFrequency(MidiNote);
    public double StartBeat { get; }
    public double DurationBeats { get; }
    public int Velocity { get; }
    public double BeatsPerMinute { get; }

    public TimeSpan StartTime => TimeSpan.FromSeconds(StartBeat * (60.0 / BeatsPerMinute));
    public TimeSpan DurationTime => TimeSpan.FromSeconds(DurationBeats * (60.0 / BeatsPerMinute));

    public double EndBeat => StartBeat < 0 ? -1 : StartBeat + DurationBeats;

    public InstrumentExpression? Instrument { get; }

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
}

public sealed class NoteCollection : IReadOnlyList<NoteExpression>
{
    public int Count => Notes.Count;

    public NoteExpression this[int index] =>  Notes[index];

    private List<NoteExpression> Notes { get; }
    private static Comparison<NoteExpression> MelodyNoteComparer = (a, b) => a.StartTime.CompareTo(b.StartTime);

    public NoteCollection(IEnumerable<NoteExpression> notes)
    {
        var newNotes = new List<NoteExpression>();
        var i = 0;
        foreach(var note in notes)
        {     
            if(note.StartBeat < 0 && i == 0)
            {
                newNotes.Add(note.WithStartBeat(0));
            }
            else if(note.StartBeat < 0 && i > 0)
            {
                // If the note has no start, set it to the end of the previous note
                var prev = newNotes[i - 1];
                newNotes.Add(note.WithStartBeat(prev.StartBeat + prev.DurationBeats));
            }
            else
            {
                newNotes.Add(note);
            }
            i++;
        }
        Notes = newNotes;
    }

    public void Sort()
    {
        Notes.Sort(MelodyNoteComparer);
    }

    public static NoteCollection Create(params NoteExpression[] notes)
        => new(notes);

    // Returns the end time (start + duration) of the collection
    public double GetEndBeat() => Notes.Count == 0
        ? 0
        : Notes.Max(n => n.StartBeat + n.DurationBeats);

    // AddSequential: shift all incoming notes by this collection's end
    public NoteCollection AddSequential(NoteCollection next, int toRemove = 0)
    {
        double offset = GetEndBeat();

        var myNotes = Notes.ToList();
        while (toRemove > 0)
        {
            // If the last note is a rest, remove it
            offset-= myNotes.Last().DurationBeats;
            myNotes.RemoveAt(myNotes.Count - 1);
            toRemove--;
        }
        var shifted = next.Notes.Select(n => n.WithStartBeat(n.StartBeat + offset));
        return new NoteCollection(myNotes.Concat(shifted));
    }

    // AddParallel: overlays, just combines the two sets
    public NoteCollection AddParallel(NoteCollection other)
        => new(Notes.Concat(other.Notes));

    public NoteCollection Repeat(int count)
    {
        if (count < 1) return new([]);
        var result = new List<NoteExpression>();
        double loopLen = GetEndBeat();
        for (int i = 0; i < count; i++)
        {
            double offset = i * loopLen;
            result.AddRange(Notes.Select(n => n.WithStartBeat(n.StartBeat + offset)));
        }
        return new(result);
    }

    // Map helpers
    public NoteCollection WithInstrument(InstrumentExpression instrument) => new(Notes.Select(n => n.WithInstrument(instrument)));
    public NoteCollection WithInstrumentIfNull(InstrumentExpression instrument) => new(Notes.Select(n => n.WithInstrumentIfNull(instrument)));
    public NoteCollection WithOctave(int octaveDelta) => new(Notes.Select(n => n.WithOctave(octaveDelta)));
    public NoteCollection WithVelocity(int velocity) => new(Notes.Select(n => n.WithVelocity(velocity)));
    public NoteCollection WithDuration(double beats) => new(Notes.Select(n => n.WithDuration(beats)));

    public NoteCollection AddRest(double beats) => this.AddSequential(Create(NoteExpression.Rest(0, beats)));
    public static NoteCollection Rest(double startBeat, double beats) => new([NoteExpression.Rest(startBeat, beats)]);

    public IEnumerator<NoteExpression> GetEnumerator() => Notes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Notes.GetEnumerator();
}


public interface INoteSource : IReadOnlyList<NoteExpression>
{
    double BeatsPerMinute { get; }
}

public class ListNoteSource : List<NoteExpression>, INoteSource
{
    public double BeatsPerMinute { get; init; } = 60;
}

public class Song : INoteSource
{
    public NoteCollection Notes { get; protected set; }
    public int Count => Notes.Count;
    public NoteExpression this[int index] => Notes[index];
    public Song(NoteCollection notes, double bpm = 120)
    {
        BeatsPerMinute = bpm;
        Notes = new NoteCollection(notes.Select(n => NoteExpression.Create(n.MidiNote,n.StartBeat, n.DurationBeats, bpm, n.Velocity,n.Instrument)));
        Notes.Sort();
    }

 
    public double BeatsPerMinute { get; private init; }



    // Exports notes, skips velocity == 0 (rest), sorted by StartBeat

    public IEnumerator<NoteExpression> GetEnumerator() => Notes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Notes.GetEnumerator();
}

