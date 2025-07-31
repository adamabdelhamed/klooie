namespace klooie;

using System.Collections.Generic;

public class MelodyPlayer
{
    private readonly ListNoteSource notes;
    private readonly double bpm;

    public MelodyPlayer(ListNoteSource notes, double bpm)
    {
        this.notes = notes;
        this.bpm = bpm;
    }

    public void PlayFrom(double startBeat, ILifetime? playLifetime = null)
    {
        var subset = new ListNoteSource() { BeatsPerMinute = bpm };

        // TODO: I should not have to set the BPM for each note, but this is a quick fix
        for (int i = 0; i < notes.Count; i++)
        {
            notes[i].BeatsPerMinute = bpm;
        }

        notes.SortMelody();
        for (int i = 0; i < notes.Count; i++)
        {
            NoteExpression? n = notes[i];
            double endBeat = n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : 0);
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, bpm, n.Velocity, n.Instrument));
        }

        if(subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(subset), playLifetime);
    }
}
