using klooie;

public interface IAudioNoteInput : ILifetime
{
    Event<IMidiEvent> EventFired { get; }
    void Start();
    void Stop();
}
