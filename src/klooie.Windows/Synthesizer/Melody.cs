namespace klooie;
public class Melody
{
    public List<Note> Notes { get; set; } = new();

    public Melody() { }

    public Melody(IEnumerable<Note> notes)
    {
        Notes.AddRange(notes);
    }

    public TimeSpan Duration
    {
        get
        {
            var max = TimeSpan.Zero;
            for (var i = 0; i < Notes.Count; i++)
            {
                var note = Notes[i];
                if (note.Start + note.Duration > max)
                {
                    max = note.Start + note.Duration;
                }
            }
            return max;
        }
    }

    public void AddNote(int midiNode, TimeSpan start, TimeSpan duration, int velocity)
    {
        Notes.Add(new Note { MidiNode = midiNode, Start = start, Duration = duration, Velocity = velocity });
    }

    public void AddNoteAfterLast(int midiNode, TimeSpan delay, TimeSpan duration, int velocity)
    {
        TimeSpan start = Notes.LastOrDefault()?.Start + Notes.LastOrDefault()?.Duration + delay ?? TimeSpan.Zero;
        Notes.Add(new Note { MidiNode = midiNode, Start = start, Duration = duration, Velocity = velocity });
    }
}

public class Note
{
    public int MidiNode { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }
    public int Velocity { get; set; }
}
