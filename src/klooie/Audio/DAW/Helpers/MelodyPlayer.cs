namespace klooie;

using System.Collections.Generic;

public class MelodyPlayer
{
    private readonly ListNoteSource notes;

    public MelodyPlayer(ListNoteSource notes)
    {
        this.notes = notes;
    }

    public void PlayFrom(double startBeat, ILifetime? playLifetime = null)
    {
        var subset = new ListNoteSource() { BeatsPerMinute = notes.BeatsPerMinute };

        // TODO: I should not have to set the BPM for each note, but this is a quick fix
        for (int i = 0; i < notes.Count; i++)
        {
            notes[i].BeatsPerMinute = notes.BeatsPerMinute;
        }

        notes.SortMelody();
        for (int i = 0; i < notes.Count; i++)
        {
            NoteExpression? n = notes[i];
            double endBeat = n.DurationBeats >= 0 ? n.StartBeat + n.DurationBeats : double.PositiveInfinity;
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.DurationBeats >= 0 && n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, notes.BeatsPerMinute, n.Velocity, n.Instrument));
        }

        if(subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(subset, notes.BeatsPerMinute), playLifetime);
    }
}
