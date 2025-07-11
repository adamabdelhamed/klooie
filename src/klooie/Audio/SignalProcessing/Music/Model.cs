using System;
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
    public double StartBeat { get; }
    public double DurationBeats { get; }
    public int Velocity { get; }

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

    public static NoteExpression Create(int midi, double startBeat, double durationBeats, int velocity = 127, InstrumentExpression? instrument = null)
        => new(midi, startBeat, durationBeats, velocity, instrument);

    public static NoteExpression Create(int midi, double durationBeats, int velocity = 127, InstrumentExpression? instrument = null)
    => new(midi, -1, durationBeats, velocity, instrument);

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

    public override string ToString() => $"Note(Midi: {MidiNote}, Start: {StartBeat}, Duration: {DurationBeats}, Velocity: {Velocity})";
}

public sealed class NoteCollection
{
    public IReadOnlyList<NoteExpression> Notes { get; }

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
        Notes = newNotes.AsReadOnly();
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
}

// ────────────────────────────────
// 3. Song: Holds a NoteCollection, exports notes
// ────────────────────────────────
public class Song
{
    public NoteCollection Notes { get; protected set; }
    public Song(NoteCollection notes, double bpm = 120)
    {
        BeatsPerMinute = bpm;
        Notes = notes;
    }

    protected Song(double bpm = 120)
    {
        BeatsPerMinute = bpm;
    }

    public double BeatsPerMinute { get; private init; }

    // Exports notes, skips velocity == 0 (rest), sorted by StartBeat
    public List<Note> Render()
    {
        double beatLen = 60.0 / BeatsPerMinute;
        var ret = Notes.Notes
            .OrderBy(expr => expr.StartBeat)
            .Select(expr =>
            {
                var patch = expr.Instrument?.PatchFunc.Invoke();
                return Note.Create(
                    expr.MidiNote,
                    TimeSpan.FromSeconds(expr.StartBeat * beatLen),
                    TimeSpan.FromSeconds(expr.DurationBeats * beatLen),
                    expr.Velocity,
                    patch
                );
            })
            .ToList();
        return ret;
    }
}

