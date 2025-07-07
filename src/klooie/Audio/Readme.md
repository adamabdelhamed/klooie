As of the time of this writing there is no good cross-platform audio library for .NET. 

For now, klooie does support sound, but only on Windows. You will need to use the [klooie.Windows](https://www.nuget.org/packages/klooie.Windows) package which is separate from the main klooie package.

In klooie.Windows you will find a class called AudioPlaybackEngine. This class uses [NAudio](https://github.com/naudio/NAudio) to play sound. The ISoundProvider interface makes it easy to use this engine from a klooie application.

```cs
using klooie;
using PowerArgs;
namespace klooie.Samples;

public class MySoundEngine : AudioPlaybackEngine
{
    // Look in the folder where this sample lives. You will find a resource file
    // called SoundEffects. That is what is being referenced below. By using a resource
    // file all our MP3 sound effects get bundled with the application and can be referred
    // to in a strongly typed way as seen in the sample. 
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
            Sound.Loop(nameof(SoundEffects.Beep), soundLifetime);
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

```

### Synthesizer patches and effects

Klooie ships with several ready-made synthesizer patches that can be used when
playing procedural music.  Each patch is a recyclable object so creating notes
incurs almost no runtime allocation.  A patch can also be extended with effects
that follow the same pattern.

```csharp
var lead = SynthPatches.CreateLead().WithTremolo();
var pad  = SynthPatches.CreateRhythmicPad();
var kick = SynthPatches.CreateKick();
var snare = SynthPatches.CreateSnare();
```

Effects such as reverb, delay, stereo chorus, tremolo and a high pass filter can
be chained via extension methods like `WithReverb()` or `WithHighPass()`.
```
