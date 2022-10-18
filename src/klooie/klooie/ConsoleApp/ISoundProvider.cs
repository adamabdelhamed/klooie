namespace klooie;
public interface ISoundProvider
{
    float MasterVolume { get; set; }
    float NewPlaySoundVolume { get; set; }
    void Play(string sound);
    void Loop(string sound, ILifetimeManager duration);
    void EndAllLoops();
    void Pause();
    void Resume();
}

public class NoOpSoundProvider : ISoundProvider
{
    public float NewPlaySoundVolume { get; set; } = 1;
    public float MasterVolume { get; set; } = 1;
    public void Loop(string sound, ILifetimeManager duration) { }
    public void Play(string sound) { }
    public void EndAllLoops() { }

    public void Pause() { }

    public void Resume() { }
}

public static class SoundProvider
{
    public static ISoundProvider Current = new NoOpSoundProvider();
}