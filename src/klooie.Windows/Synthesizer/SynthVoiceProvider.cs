using NAudio.Wave;

namespace klooie;



public class SynthVoiceProvider : RecyclableAudioProvider, IReleasableNote
{
    private ISignalSource source;
    public static readonly LazyPool<SynthVoiceProvider> _pool = new(() => new SynthVoiceProvider());
    public bool IsDone => source.IsDone;
    private SynthVoiceProvider() { }

    public static SynthVoiceProvider Create(ISignalSource source)
    {
        var ret = _pool.Value.Rent();
        ret.source = source;
        return ret;
    }
    private WaveFormat waveFormat = new WaveFormat(SoundProvider.SampleRate, SoundProvider.BitsPerSample, SoundProvider.ChannelCount);
    public override WaveFormat WaveFormat => waveFormat;

    public void ReleaseNote() => source.ReleaseNote();

    public override int Read(float[] buffer, int offset, int count)
    {
        return source.Render(buffer, offset, count);
    }

    protected override void OnReturn()
    {
        (source as Recyclable)?.TryDispose();
        source = null;
        base.OnReturn();
    }
}


