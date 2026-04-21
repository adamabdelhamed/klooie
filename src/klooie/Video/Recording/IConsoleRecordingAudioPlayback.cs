namespace klooie;

public interface IConsoleRecordingAudioPlayback : IDisposable
{
    void Load(ConsoleRecordingSessionReader sessionReader);
    void PlayFrom(TimeSpan position);
    void Pause();
    void Stop();
}
