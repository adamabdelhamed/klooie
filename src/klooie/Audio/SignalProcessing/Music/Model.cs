using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class NoteExpression
{
    public int MidiNote { get; init; }
    public double DurationBeats { get; init; }
    public int Velocity { get; init; }
    public Func<ISynthPatch>? PatchFunc { get; init; }

    public static NoteExpression Create(int midi, double beats, int vel = 127, Func<ISynthPatch>? patchFunc = null)
        => new NoteExpression() { MidiNote = midi, DurationBeats = beats, Velocity = vel, PatchFunc = patchFunc };
}

public class Motif
{
    public readonly NoteExpression[] Notes;
    public readonly string? Name;
    public readonly double OffsetBeats;
    public Func<ISynthPatch>? DefaultPatchFunc;

    public Motif(IEnumerable<NoteExpression> notes, Func<ISynthPatch>? defaultPatchFunc = null, double offsetBeats = 0f, string? name = null)
    {
        Notes = notes.Select(n => NoteExpression.Create(n.MidiNote, n.DurationBeats, n.Velocity, n.PatchFunc ?? defaultPatchFunc)).ToArray();
        Name = name;
        OffsetBeats = offsetBeats;
        DefaultPatchFunc = defaultPatchFunc;
    }

    public static IEnumerable<Motif> Repeat(Motif motif, int count)
    {
        var motifBeats = motif.Notes.Sum(n => n.DurationBeats);
        double t = 0;
        for (int i = 0; i < count; i++)
        {
            yield return new Motif(motif.Notes, motif.DefaultPatchFunc, t, motif.Name);
            t += motifBeats;
        }
    }
}


public class Phrase
{
    public readonly Motif[] Motifs;
    public readonly string? Name;
    public readonly double OffsetBeats;

    public double GetLength() => Motifs.Max(m => m.OffsetBeats + m.Notes.Sum(n => n.DurationBeats));


    public Phrase(string name, IEnumerable<Motif> motifs, double offsetBeats = 0)
    {
        Motifs = motifs.ToArray();
        Name = name;
        OffsetBeats = offsetBeats;
    }

    public static IEnumerable<Phrase> Repeat(Phrase phrase, int count)
    {
        double offset = 0;
        for (int i = 0; i < count; i++)
        {
            yield return new Phrase(phrase.Name, phrase.Motifs, offsetBeats: offset);
            offset += phrase.GetLength();
        }
    }
}

 

public class Section
{
    public readonly Phrase[] Phrases;
    public readonly string Name;
    public readonly double OffsetBeats;

    public double GetLength()
        => Phrases.Sum(p => p.Motifs.Max(m => m.OffsetBeats + m.Notes.Sum(n => n.DurationBeats)));

    public Section(string name, IEnumerable<Phrase> phrases, double offsetBeats = 0)
    {
        Name = name;
        Phrases = phrases.ToArray();
        OffsetBeats = offsetBeats;
    }
}

public class Song
{
    public readonly Section[] Sections;
    public Song(IEnumerable<Section> sections) => Sections = sections.ToArray();

    public Melody Render(double bpm = 120.0)
    {
        double beatLen = 60.0 / bpm;
        var melody = Melody.Create();

        foreach (var section in Sections)
        {
            foreach (var scheduledPhrase in section.Phrases)
            {
                double phraseAbsStart = section.OffsetBeats + scheduledPhrase.OffsetBeats;

                // Export Melodic Motifs
                foreach (var scheduledMotif in scheduledPhrase.Motifs)
                {
                    double motifAbsStart = phraseAbsStart + scheduledMotif.OffsetBeats;
                    double runningBeats = motifAbsStart;

                    for (int noteIndex = 0; noteIndex < scheduledMotif.Notes.Length; noteIndex++)
                    {
                        NoteExpression n = scheduledMotif.Notes[noteIndex];
                        Func<ISynthPatch>? patchFunc = n.PatchFunc ?? scheduledMotif.DefaultPatchFunc;
                        ISynthPatch? patch = patchFunc?.Invoke();

                        melody.AddNote(
                            n.MidiNote,
                            TimeSpan.FromSeconds(runningBeats * beatLen),
                            TimeSpan.FromSeconds(n.DurationBeats * beatLen),
                            n.Velocity,
                            patch
                        );
                        runningBeats += n.DurationBeats;
                    }
                }


            }
        }
        return melody;
    }
}

public class SongBuilder
{
    private readonly List<Section> _sections = new();
    private double _offset = 0;

    private SongBuilder() { }

    public static SongBuilder Begin() => new SongBuilder();

    public SongBuilder AddSection(string name, Phrase phrase, int repeat = 1)
    {
        var repeated = Phrase.Repeat(phrase, repeat);
        var section = new Section(name, repeated, _offset);
        _sections.Add(section);
        _offset += section.GetLength();
        return this;
    }

    // Overload to accept multiple phrases (optional)
    public SongBuilder AddSection(string name, IEnumerable<Phrase> phrases, int repeat = 1)
    {
        var allPhrases = phrases.SelectMany(p => Phrase.Repeat(p, repeat));
        var section = new Section(name, allPhrases, _offset);
        _sections.Add(section);
        _offset += section.GetLength();
        return this;
    }

    public List<Section> Build() => _sections;
}

