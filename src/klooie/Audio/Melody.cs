namespace klooie;
public class Melody : Recyclable
{
    public List<Note> Notes { get; set; } = new();
    private Melody() { }

    private static LazyPool<Melody> _pool = new(() => new Melody());
    public static Melody Create(IEnumerable<Note> notes)
    {
        var melody = _pool.Value.Rent();
        melody.Notes.AddRange(notes);
        return melody;
    }

    public static Melody Create()
    {
        var melody = _pool.Value.Rent();
        return melody;
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

    public void AddNote(int midiNode, TimeSpan start, TimeSpan duration, int velocity, ISynthPatch? patch)
    {
        Notes.Add(new Note { MidiNode = midiNode, Start = start, Duration = duration, Velocity = velocity, Patch = patch });
    }

    protected override void OnReturn()
    {
        Notes.Clear();
        base.OnReturn();
    }
}

public class Note
{
    public int MidiNode { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }
    public int Velocity { get; set; }
    public ISynthPatch? Patch { get; set; }
}
