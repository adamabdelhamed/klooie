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
    private static LazyPool<VolumeKnob> pool = new LazyPool<VolumeKnob>(() => new VolumeKnob());
    private VolumeKnob() { }
    public static VolumeKnob Create()
    {
        var knob = pool.Value.Rent();
        knob.Volume = 1f; 
        return knob;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        volumeChanged?.Dispose();
        volumeChanged = null;
        volume = 0f; 
    }
}
