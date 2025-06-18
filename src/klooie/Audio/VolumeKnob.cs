namespace klooie;
public class VolumeKnob : Recyclable
{
    private Event<float>? volumeChanged;
    public Event<float> VolumeChanged => volumeChanged ??= Event<float>.Create();

    private float volume;
    public float Volume
    {
        get
        {
            return volume;
        }
        set
        {
            var newVolume = Math.Max(0, Math.Min(1, value));
            if (newVolume == volume) return;
            volume = newVolume;
            volumeChanged?.Fire(newVolume);
        }
    }

    private float pan;
    public float Pan
    {
        get => pan;
        set
        {
            var newPan = Math.Max(-1, Math.Min(1, value));
            if (newPan == pan) return;
            pan = newPan;
            panChanged?.Fire(newPan);
        }
    }

    private Event<float>? panChanged;
    public Event<float> PanChanged => panChanged ??= Event<float>.Create();


    private static LazyPool<VolumeKnob> pool = new LazyPool<VolumeKnob>(() => new VolumeKnob());
    protected VolumeKnob() { }
    public static VolumeKnob Create()
    {
        var knob = pool.Value.Rent();
        knob.Volume = 1f;
        knob.Pan = 0f; // Center pan by default
        return knob;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        volumeChanged?.Dispose();
        volumeChanged = null;
        volume = 0f;
        panChanged?.Dispose();
        panChanged = null;
        pan = 0f;
    }
}
