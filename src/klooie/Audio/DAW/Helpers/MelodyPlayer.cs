namespace klooie;

using System.Collections.Generic;

public class MelodyPlayer
{
    private readonly INoteSource notes;
    private readonly double bpm;

    public MelodyPlayer(INoteSource notes, double bpm)
    {
        this.notes = notes;
        this.bpm = bpm;
    }

    public void PlayFrom(double startBeat, ILifetime? playLifetime = null)
    {
        var subset = new List<NoteExpression>();
        foreach(var n in notes)
        {
            double endBeat = n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : 0);
            if (endBeat <= startBeat) continue;

            double relStart = n.StartBeat - startBeat;
            if (relStart < 0) relStart = 0;

            double duration = n.DurationBeats;
            if (n.StartBeat < startBeat)
            {
                duration = endBeat - startBeat;
            }

            subset.Add(NoteExpression.Create(n.MidiNote, relStart, duration, n.Velocity, n.Instrument));
        }

        if(subset.Count == 0) return;

        ConsoleApp.Current.Sound.Play(new Song(new NoteCollection(subset), bpm), playLifetime);
    }
}
