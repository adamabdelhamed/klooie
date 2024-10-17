namespace ScrollSucker;
public class ScrollSuckerSoundEngine : AudioPlaybackEngine
{
    protected override Dictionary<string, byte[]> LoadSounds() =>
        ResourceFileSoundLoader.LoadSounds<SoundEffects>();
}