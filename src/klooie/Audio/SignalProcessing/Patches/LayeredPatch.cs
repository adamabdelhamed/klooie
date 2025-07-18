using klooie;

[SynthCategory("Utility")]
[SynthDescription("""
Allows several patches to be played together as layers.  Each layer
has its own volume, stereo pan and pitch offset so you can build rich,
multi-voice instruments.
""")]
public sealed class LayeredPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch[] layers;
    private float[] layerVolumes;
    private float[] layerPans;
    private int[] layerTransposes;
    public ISynthPatch[] Layers => layers;
    public float[] LayerVolumes => layerVolumes;
    public float[] LayerPans => layerPans;
    public int[] LayerTransposes => layerTransposes;

    public ISynthPatch InnerPatch => layers.Length > 0 ? layers[0] : null;

    private static readonly LazyPool<LayeredPatch> _pool = new(() => new LayeredPatch());

    private LayeredPatch() { }

    public static LayeredPatch Create(Settings settings)
    {
        var p = _pool.Value.Rent();
        int count = settings.Patches.Length;

        p.layers = settings.Patches;
        p.layerVolumes = ValidateOrCreate(settings.Volumes, count, 1f);
        p.layerPans = ValidateOrCreate(settings.Pans, count, 0f);
        p.layerTransposes = ValidateOrCreate(settings.Transposes, count, 0);

        return p;
    }

    // Optional: maintain legacy overload for convenience
    public static LayeredPatch Create(
        ISynthPatch[] patches,
        float[]? volumes = null,
        float[]? pans = null,
        int[]? transposes = null)
        => Create(new Settings
        {
            Patches = patches,
            Volumes = volumes,
            Pans = pans,
            Transposes = transposes
        });

    private static float[] ValidateOrCreate(float[]? arr, int count, float def)
    {
        if (arr != null && arr.Length == count) return arr;
        var a = new float[count];
        for (int i = 0; i < count; i++) a[i] = def;
        return a;
    }
    private static int[] ValidateOrCreate(int[]? arr, int count, int def)
    {
        if (arr != null && arr.Length == count) return arr;
        var a = new int[count];
        for (int i = 0; i < count; i++) a[i] = def;
        return a;
    }

    // Proxy properties from the first layer
    public WaveformType Waveform => layers[0].Waveform;
    public float DriftFrequencyHz => layers[0].DriftFrequencyHz;
    public float DriftAmountCents => layers[0].DriftAmountCents;
    public bool EnablePitchDrift => layers[0].EnablePitchDrift;
    public bool EnableSubOsc => layers[0].EnableSubOsc;
    public int SubOscOctaveOffset => layers[0].SubOscOctaveOffset;
    public float SubOscLevel => layers[0].SubOscLevel;
    public bool EnableTransient => layers[0].EnableTransient;
    public float TransientDurationSeconds => layers[0].TransientDurationSeconds;
    public int Velocity => layers[0].Velocity;
    public RecyclableList<IEffect> Effects => layers[0].Effects;

    public bool EnableVibrato => layers[0].EnableVibrato;
    public float VibratoRateHz => layers[0].VibratoRateHz;
    public float VibratoDepthCents => layers[0].VibratoDepthCents;
    public float VibratoPhaseOffset => layers[0].VibratoPhaseOffset;

    public void GetPatches(List<ISynthPatch> patches)
        => patches.AddRange(layers);

    public bool IsNotePlayable(int midiNote)
    {
        foreach (var l in layers)
            if (l?.IsNotePlayable(midiNote) ?? true)
                return true;
        return false;
    }

    public void SpawnVoices(
        float freq,
        VolumeKnob master,
        NoteExpression note,
        List<SynthSignalSource> outVoices)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            float transFreq = (layerTransposes[i] == 0)
                ? freq
                : freq * MathF.Pow(2f, layerTransposes[i] / 12f);

            layers[i].SpawnVoices(transFreq, master, note, outVoices);
        }
    }

    protected override void OnReturn()
    {
        if (layers != null)
            foreach (var l in layers)
                if (l is Recyclable r) r.TryDispose();

        layers = null!;
        layerVolumes = null!;
        layerPans = null!;
        layerTransposes = null!;
        base.OnReturn();
    }

    [SynthDescription("""
Configuration describing which patches are layered together and
how each layer is mixed in terms of volume, pan position and
transpose amount.
""")]
    public struct Settings
    {
        [SynthDescription("""
Array of patches that will play at the same time.  The
index of each patch matches the entries in Volumes, Pans and Transposes.
""")]
        public ISynthPatch[] Patches;

        [SynthDescription("""
Relative volume of each layer from 0 to 1.  Values
above 1 will boost that layer while values below 1 reduce it.
""")]
        public float[]? Volumes;

        [SynthDescription("""
Stereo pan for each layer where -1 is full left and
+1 is full right.
""")]
        public float[]? Pans;

        [SynthDescription("""
Number of semitones each layer is transposed
relative to the original pitch.
""")]
        public int[]? Transposes;
    }
}
