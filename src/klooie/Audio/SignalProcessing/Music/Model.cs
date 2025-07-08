using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class Motif
{
    public readonly (int MidiNote, double DurationBeats, int Velocity, Func<ISynthPatch>? PatchFunc)[] Notes;
    public readonly string? Name;
    public readonly Func<ISynthPatch>? DefaultPatchFunc;

    public Motif(
        (int midi, double beats, int vel, Func<ISynthPatch>? patchFunc)[] notes,
        string? name = null,
        Func<ISynthPatch>? defaultPatchFunc = null)
    {
        Notes = notes;
        Name = name;
        DefaultPatchFunc = defaultPatchFunc;
    }

    // Helper: All notes use the same patch
    public static Motif Create(string name, Func<ISynthPatch>? patchFunc, params (int midi, double beats, int vel)[] notes)
        => new Motif(notes.Select(n => (n.midi, n.beats, n.vel, (Func<ISynthPatch>?)null)).ToArray(), name, patchFunc);
}

public class DrumNote
{
    public readonly int MidiNote;
    public readonly double DurationBeats;
    public readonly int Velocity;
    public readonly Func<ISynthPatch> PatchFunc;

    public DrumNote(int midi, double beats, int vel, Func<ISynthPatch> patchFunc)
        => (MidiNote, DurationBeats, Velocity, PatchFunc) = (midi, beats, vel, patchFunc);
}

public class DrumMotif
{
    public readonly DrumNote[] Notes;
    public readonly string? Name;

    public DrumMotif(DrumNote[] notes, string? name = null)
        => (Notes, Name) = (notes, name);

    public static (DrumMotif motif, double offsetBeats)[] TileDrumMotif(DrumMotif motif, double totalBeats)
    {
        var motifBeats = motif.Notes.Sum(n => n.DurationBeats);
        var result = new List<(DrumMotif, double)>();
        double t = 0;
        while (t + 0.0001 < totalBeats) // floating point fudge
        {
            result.Add((motif, t));
            t += motifBeats;
        }
        return result.ToArray();
    }
}


public class Phrase
{
    public readonly (Motif motif, double offsetBeats)[] MelodicMotifs;
    public readonly (DrumMotif motif, double offsetBeats)[] DrumMotifs;
    public readonly string? Name;

    public Phrase(
        (Motif motif, double offsetBeats)[] melodic,
        (DrumMotif motif, double offsetBeats)[] drums,
        string? name = null)
    {
        MelodicMotifs = melodic;
        DrumMotifs = drums;
        Name = name;
    }
}

public class Section
{
    public readonly (Phrase phrase, double offsetBeats)[] Phrases;
    public readonly string Name;
    public Section(string name, (Phrase, double)[] phrases)
    {
        Name = name;
        Phrases = phrases;
    }
}

public class Song
{
    public readonly (Section section, double offsetBeats)[] Sections;
    public Song((Section, double)[] sections) => Sections = sections;
}

public static class SongExporter
{
    public static Melody ExportToMelody(Song song, double bpm = 120.0)
    {
        double beatLen = 60.0 / bpm;
        var melody = Melody.Create();

        foreach (var (section, sectionOffsetBeats) in song.Sections)
        {
            foreach (var (phrase, phraseOffsetBeats) in section.Phrases)
            {
                double phraseAbsStart = sectionOffsetBeats + phraseOffsetBeats;

                // Export Melodic Motifs
                foreach (var (motif, motifOffsetBeats) in phrase.MelodicMotifs)
                {
                    double motifAbsStart = phraseAbsStart + motifOffsetBeats;
                    double runningBeats = motifAbsStart;

                    for (int noteIndex = 0; noteIndex < motif.Notes.Length; noteIndex++)
                    {
                        (int MidiNote, double DurationBeats, int Velocity, Func<ISynthPatch>? PatchFunc) n = motif.Notes[noteIndex];
                        Func<ISynthPatch>? patchFunc = n.PatchFunc ?? motif.DefaultPatchFunc;
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

                // Export Drum Motifs
                foreach (var (drumMotif, drumMotifOffsetBeats) in phrase.DrumMotifs)
                {
                    double drumMotifAbsStart = phraseAbsStart + drumMotifOffsetBeats;
                    double runningBeats = drumMotifAbsStart;

                    for (int i = 0; i < drumMotif.Notes.Length; i++)
                    {
                        DrumNote? n = drumMotif.Notes[i];
                        // For drums, patchFunc is always required
                        ISynthPatch patch = n.PatchFunc.Invoke();
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


