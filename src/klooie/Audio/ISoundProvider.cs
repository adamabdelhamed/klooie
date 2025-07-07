using klooie.Gaming;

namespace klooie;

public interface IReleasableNote
{
    void ReleaseNote();
}

public static class SoundProvider
{
    public const int SampleRate = 44100;
    public const int ChannelCount = 2;
    public const int BitsPerSample = 16;
    public static ISoundProvider Current { get; set; }
}


public interface ISoundProvider
{
    VolumeKnob MasterVolume { get;  }
    void Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null);
    void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null);
    void Pause();
    void Resume();
    void ClearCache();
    long SamplesRendered { get; }
    public IReleasableNote? PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null);
    public IReleasableNote? PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null);
    public void ScheduleSynthNote(int midiNote, long startSample, double durationSeconds, float velocity = 1.0f, SynthPatch patch = null);
    EventLoop EventLoop { get; }
}

public class NoOpSoundProvider : ISoundProvider
{
    public EventLoop EventLoop => ConsoleApp.Current;
    public VolumeKnob MasterVolume { get; set; }
    public void Loop(string? sound, ILifetime? duration = null, VolumeKnob? volumeKnob = null) { }
    public void Play(string? sound, ILifetime? maxDuration = null, VolumeKnob? volumeKnob = null) { }
    public void Pause() { }
    public void Resume() { }
    public void ClearCache() { }
    public long SamplesRendered => 0;
    public IReleasableNote? PlayTimedNote(float frequencyHz, double durationSeconds, SynthPatch patch, VolumeKnob? knob = null)
    {
        return null;
    }

    public IReleasableNote? PlaySustainedNote(float frequencyHz, SynthPatch patch, VolumeKnob? knob = null)
    {
        return null;
    }

    public void ScheduleSynthNote(int midiNote, long startSample, double durationSeconds, float velocity = 1.0f, SynthPatch patch = null)
    {
        // No-op implementation
    }
}

public class SynthPatch : Recyclable
{
    private SynthPatch() { }
    private static LazyPool<SynthPatch> _pool = new(() => new SynthPatch());
    public static SynthPatch Create()
    {
        var patch = _pool.Value.Rent();
        patch.Waveform = WaveformType.Sine; // default waveform
        patch.Envelope = ADSREnvelope.Create();
        patch.EnableTransient = false;
        patch.TransientDurationSeconds = 0.01f; // default transient duration
        patch.EnableLowPassFilter = false;
        patch.FilterAlpha = 0.05f; // default filter alpha
        patch.EnableDynamicFilter = false;
        patch.FilterBaseAlpha = 0.01f; // default base alpha
        patch.FilterMaxAlpha = 0.2f; // default max alpha
        patch.EnableSubOsc = false; // default sub-oscillator disabled
        patch.SubOscLevel = 0.5f; // default sub-oscillator level
        patch.SubOscOctaveOffset = -1; // default sub-oscillator one octave below
        patch.EnableDistortion = false; // default distortion disabled
        patch.DistortionAmount = 0.8f; // default distortion amount
        patch.EnablePitchDrift = false; // default pitch drift disabled
        patch.DriftFrequencyHz = 0.5f; // default drift frequency
        patch.DriftAmountCents = 5f; // default drift amount in cents
        patch.Velocity = 1f; // default velocity
        return patch;
    }

    public WaveformType Waveform;
    public ADSREnvelope Envelope;
    public bool EnableTransient;
    public float TransientDurationSeconds;
    public bool EnableLowPassFilter;
    public float FilterAlpha;

    public bool EnableDynamicFilter;
    public float FilterBaseAlpha;
    public float FilterMaxAlpha;

    public bool EnableSubOsc;
    public float SubOscLevel; // 0 = silent, 1 = same as main
    public int SubOscOctaveOffset; // usually -1 for one octave below

    public bool EnableDistortion;
    public float DistortionAmount; // 0 = clean, 1 = hard clip

    public bool EnablePitchDrift;
    public float DriftFrequencyHz; // how fast the pitch wobbles
    public float DriftAmountCents;   // how wide it wobbles (cents = 1/100 semitone)
    public float Velocity; // default full velocity

    protected override void OnReturn()
    {
        base.OnReturn();
        Envelope.Dispose();
        Envelope = null!;
    }
}

public static class SynthPatches
{
    public static SynthPatch CreateGuitar()
    {
        var ret = SynthPatch.Create();
        ret.Waveform = WaveformType.PluckedString; // New waveform
        ret.EnableTransient = false;               // Let the pluck algorithm handle the burst
        ret.EnableLowPassFilter = true;            // Smooth the highs
        ret.EnableDynamicFilter = false;           // Keep static filtering for simplicity
        ret.FilterAlpha = 0.02f;
        ret.EnableSubOsc = false;                  // Skip for realism
        ret.EnableDistortion = false;              // Turn off harshness
        ret.EnablePitchDrift = false;              // Plucked strings are stable
        ret.TransientDurationSeconds = 0.01f;      // Ignored here but safe to leave
        ret.Envelope.Attack = 0.005;
        ret.Envelope.Decay = 0.1;
        ret.Envelope.Sustain = 0.25;
        ret.Envelope.Release = 0.4;
        return ret;
    }

    public static SynthPatch CreateBass()
    {
        var ret = SynthPatch.Create();
        ret.Waveform = WaveformType.Sine; // Smooth bass sound
        ret.EnableTransient = false; // No transient burst needed
        ret.EnableLowPassFilter = true; // Smooth out highs
        ret.EnableDynamicFilter = false; // Keep static filtering
        ret.FilterAlpha = 0.01f; // Let more mids through post-distortion
        ret.EnableSubOsc = true; // Add sub-oscillator for depth
        ret.SubOscLevel = 0.6f; // Moderate sub-oscillator level
        ret.SubOscOctaveOffset = -1; // One octave below
        ret.EnableDistortion = true; // Add some growl
        ret.DistortionAmount = 0.2f; // Just enough for growl
        ret.EnablePitchDrift = false; // Stable bass
        ret.Envelope.Attack = 0.005;
        ret.Envelope.Decay = 0.12;
        ret.Envelope.Sustain = 0.5; // Increase sustain to avoid piano dropoff
        ret.Envelope.Release = 0.4;
        return ret;
    }
}

public enum WaveformType
{
    Sine,
    Square,
    Saw,
    Triangle,
    Noise,
    PluckedString,
}