namespace klooie;
public interface ISoundProvider
{
    float MasterVolume { get; set; }
    float NewPlaySoundVolume { get; set; }
    void Play(string? sound, ILifetime? maxDuration = null);
    void Loop(string? sound, ILifetime? duration = null);
    void EndAllLoops();
    void Pause();
    void Resume();
}

public class NoOpSoundProvider : ISoundProvider
{
    public float NewPlaySoundVolume { get; set; } = 1;
    public float MasterVolume { get; set; } = 1;
    public void Loop(string? sound, ILifetime? duration = null) { }
    public void Play(string? sound, ILifetime? maxDuration = null) { }
    public void EndAllLoops() { }
    public void Pause() { }
    public void Resume() { }
}