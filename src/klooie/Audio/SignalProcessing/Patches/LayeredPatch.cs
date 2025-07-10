using System.Collections.Generic;

namespace klooie;

public sealed class LayeredPatch : Recyclable, ISynthPatch
{
    private ISynthPatch[] layers;
    private float[] layerVolumes;
    private float[] layerPans;
    public ISynthPatch[] Layers => layers;
    public float[] LayerVolumes => layerVolumes;
    public float[] LayerPans => layerPans;

    // ISynthPatch contract:
    public ISynthPatch InnerPatch => layers.Length > 0 ? layers[0] : null;

    private static readonly LazyPool<LayeredPatch> _pool =
        new(() => new LayeredPatch());

    private LayeredPatch() { }

    public static LayeredPatch Create(
        ISynthPatch[] patches,
        float[]? volumes = null,
        float[]? pans = null)
    {
        var p = _pool.Value.Rent();
        p.layers = patches;
        int count = patches.Length;
        p.layerVolumes = (volumes != null && volumes.Length == count) ? volumes : CreateArray(count, 1f);
        p.layerPans = (pans != null && pans.Length == count) ? pans : CreateArray(count, 0f);
        return p;
    }

    private static float[] CreateArray(int count, float value)
    {
        var arr = new float[count];
        for (int j = 0; j < count; j++) arr[j] = value;
        return arr;
    }

    // Proxy properties from the first layer (typical for Klooie ISynthPatch style)
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
        VolumeKnob? sampleKnob,
        List<SynthSignalSource> outVoices)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            // Fix: Don't use static lambda here!
            int layerIdx = i; // Local copy for lambdas if needed
            var layerKnob = (sampleKnob != null || layerVolumes[layerIdx] != 1f || layerPans[layerIdx] != 0f) ? VolumeKnob.Create() : null;
            if (layerKnob != null)
            {
                layerKnob.Volume = (sampleKnob?.Volume ?? 1f) * layerVolumes[layerIdx];
                layerKnob.Pan = (sampleKnob?.Pan ?? 0f) + layerPans[layerIdx];
                if (sampleKnob != null)
                {
                    // Use normal lambdas so we can capture 'layerIdx'
                    sampleKnob.VolumeChanged.Subscribe(layerKnob, (me, v) => me.Volume = v * layerVolumes[layerIdx], layerKnob);
                    sampleKnob.PanChanged.Subscribe(layerKnob, (me, v) => me.Pan = v + layerPans[layerIdx], layerKnob);
                }
                OnDisposed(layerKnob, Recyclable.TryDisposeMe);
            }
            layers[layerIdx].SpawnVoices(freq, master, layerKnob, outVoices);
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
        base.OnReturn();
    }
}
