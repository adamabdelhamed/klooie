//#Sample -Id SoundSample
using klooie;
using PowerArgs;
namespace klooie.Samples;

public class MySoundEngine : AudioPlaybackEngine
{
    // Look in the folder where this sample lives. You will find a resource file
    // called SoundEffects. That is what is being referenced below. By using a resource
    // file all our MP3 sound effects get bundled with the application and can be referred
    // to in a strongly typed was as seen in the sample. 
    protected override Dictionary<string, byte[]> LoadSounds() => 
        ResourceFileSoundLoader.LoadSounds<SoundEffects>();
}

// Define your application
public class SoundSample : ConsoleApp
{
    protected override async Task Startup()
    {
        Sound = new MySoundEngine();
        // plays the sound once
        Sound.Play(nameof(SoundEffects.SoundEffectSample));
        await Task.Delay(2500);

        using(var soundLifetime = this.CreateChildLifetime())
        {
            // plays the sound in a loop until this using block exits
            Sound.Loop(nameof(SoundEffects.beep), soundLifetime);
            await Task.Delay(5000);
        }

        // loops the sound for the lifetime of the app
        Sound.Loop(nameof(SoundEffects.SoundEffectSample), this);
        await Task.Delay(4000);
        Stop();
    }

    // Entry point for your application
    public static class SoundSampleProgram
    {
        public static void Main() => new SoundSample().Run();
    }
}
//#EndSample
