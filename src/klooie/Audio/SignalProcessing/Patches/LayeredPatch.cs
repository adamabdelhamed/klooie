using klooie;

[SynthCategory("Utility")]
[SynthDescription("""
Combines multiple patches into layers with per-layer volume, pan and
transpose control for complex instruments.
""")]
public sealed class LayeredPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch[] layers;
    private float[] layerVolumes;
    private float[] layerPans;
    private int[] layerTransposes; // <-- NEW
    public ISynthPatch[] Layers => layers;
    public float[] LayerVolumes => layerVolumes;
    public float[] LayerPans => layerPans;
    public int[] LayerTransposes => layerTransposes;

    // ISynthPatch contract:
    public ISynthPatch InnerPatch => layers.Length > 0 ? layers[0] : null;

    private static readonly LazyPool<LayeredPatch> _pool = new(() => new LayeredPatch());

    private LayeredPatch() { }

    // === New overloaded Create ===
    public static LayeredPatch Create(
        ISynthPatch[] patches,
        float[]? volumes = null,
        float[]? pans = null,
        int[]? transposes = null)
    {
        var p = _pool.Value.Rent();
        p.layers = patches;
        int count = patches.Length;
        p.layerVolumes = (volumes != null && volumes.Length == count) ? volumes : CreateArray(count, 1f);
        p.layerPans = (pans != null && pans.Length == count) ? pans : CreateArray(count, 0f);
        p.layerTransposes = (transposes != null && transposes.Length == count) ? transposes : CreateArray(count, 0);
        return p;
    }

    private static float[] CreateArray(int count, float value)
    {
        var arr = new float[count];
        for (int j = 0; j < count; j++) arr[j] = value;
        return arr;
    }
    private static int[] CreateArray(int count, int value)
    {
        var arr = new int[count];
        for (int j = 0; j < count; j++) arr[j] = value;
        return arr;
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
    {
        patches.AddRange(layers);
    }

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
            int layerIdx = i;
            // Transpose frequency for this layer
            float transFreq = (layerTransposes[layerIdx] == 0)
                ? freq
                : freq * MathF.Pow(2f, layerTransposes[layerIdx] / 12f);

            layers[layerIdx].SpawnVoices(transFreq, master, note, outVoices);
        }
    }

    protected override void OnReturn()
    {
        if (layers != null)
        {
            foreach (var l in layers)
                if (l is Recyclable r) r.TryDispose();
        }
        layers = null!;
        layerVolumes = null!;
        layerPans = null!;
        layerTransposes = null!;
        base.OnReturn();
    }

    [SynthDescription("""
    Parameters controlling how patches are layered.
    """)]
    public struct Settings
    {
        [SynthDescription("""Patches to layer together.""")]
        public ISynthPatch[] Patches;

        [SynthDescription("""Relative volume for each layer (0–1).""")]
        public float[]? Volumes;

        [SynthDescription("""Pan position per layer (-1 = left, +1 = right).""")]
        public float[]? Pans;

        [SynthDescription("""Semitone offset applied to each layer.""")]
        public int[]? Transposes;
    }
}
