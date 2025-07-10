using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace klooie;

public class PowerChordPatch : Recyclable, ISynthPatch
{
    private ISynthPatch basePatch;
    public ISynthPatch InnerPatch => basePatch;
    private int[] intervals;
    private float detuneCents;
    private float panSpread;

    private static LazyPool<PowerChordPatch> _pool = new(() => new PowerChordPatch());

    public WaveformType Waveform => basePatch.Waveform;
    public float DriftFrequencyHz => basePatch.DriftFrequencyHz;
    public float DriftAmountCents => basePatch.DriftAmountCents;
    public bool EnablePitchDrift => basePatch.EnablePitchDrift;
    public bool EnableSubOsc => basePatch.EnableSubOsc;
    public int SubOscOctaveOffset => basePatch.SubOscOctaveOffset;
    public float SubOscLevel => basePatch.SubOscLevel;
    public bool EnableTransient => basePatch.EnableTransient;
    public float TransientDurationSeconds => basePatch.TransientDurationSeconds;
    public int Velocity => basePatch.Velocity;


    public bool EnableVibrato => basePatch.EnableVibrato;
    public float VibratoRateHz => basePatch.VibratoRateHz;
    public float VibratoDepthCents => basePatch.VibratoDepthCents;
    public float VibratoPhaseOffset => basePatch.VibratoPhaseOffset;

    public RecyclableList<IEffect> Effects { get; private set; } = RecyclableListPool<IEffect>.Instance.Rent(20);

    private PowerChordPatch() { }

    public static PowerChordPatch Create(
        ISynthPatch basePatch,
        int[] intervals = null,              // e.g. [0, 7] = root + 5th; [0, 7, 12] = root+5th+octave
        float detuneCents = 6f,
        float panSpread = 0.8f)
    {
        var patch = _pool.Value.Rent();
        patch.Construct(basePatch, intervals, detuneCents, panSpread);
        return patch;
    }

    protected void Construct(
        ISynthPatch basePatch,
        int[]? intervals,
        float detuneCents,
        float panSpread)
    {
        this.basePatch = basePatch ?? throw new ArgumentNullException(nameof(basePatch));
        this.intervals = intervals ?? new int[] { 0, 7 }; // Default: power chord (root + 5th)
        this.detuneCents = detuneCents;
        this.panSpread = panSpread;
    }

    public void SpawnVoices(float frequencyHz, VolumeKnob master, VolumeKnob? sampleKnob, List<SynthSignalSource> outVoices)
    {
        int numLayers = intervals.Length;
        for (int i = 0; i < numLayers; i++)
        {
            int interval = intervals[i];
            float rel = (i - (numLayers - 1) / 2.0f);

            // Calculate detune and pan for each layer
            float detune = rel * detuneCents / Math.Max(numLayers - 1, 1);
            float pan = rel * panSpread / Math.Max(numLayers - 1, 1);

            float freq = frequencyHz * MathF.Pow(2f, interval / 12.0f) * MathF.Pow(2f, detune / 1200.0f);

            var nestedKnob = sampleKnob != null ? VolumeKnob.Create() : null;
            if (nestedKnob != null)
            {
                OnDisposed(nestedKnob, Recyclable.TryDisposeMe);
                nestedKnob.Volume = sampleKnob.Volume;
                nestedKnob.Pan = sampleKnob.Pan;
                sampleKnob.VolumeChanged.Subscribe(sampleKnob, static (me, v) => me.Volume = v, nestedKnob);
                sampleKnob.PanChanged.Subscribe(nestedKnob, static (me, v) => me.Pan = v, nestedKnob);
                nestedKnob.Pan = pan;
            }

            // Clone basePatch for each layer (deep copy of effects)
            var layerPatch = SynthPatch.Create();
            layerPatch.Waveform = basePatch.Waveform;
            layerPatch.DriftFrequencyHz = basePatch.DriftFrequencyHz;
            layerPatch.DriftAmountCents = basePatch.DriftAmountCents;
            layerPatch.EnablePitchDrift = basePatch.EnablePitchDrift;
            layerPatch.EnableSubOsc = basePatch.EnableSubOsc;
            layerPatch.SubOscOctaveOffset = basePatch.SubOscOctaveOffset;
            layerPatch.SubOscLevel = basePatch.SubOscLevel;
            layerPatch.EnableTransient = basePatch.EnableTransient;
            layerPatch.TransientDurationSeconds = basePatch.TransientDurationSeconds;
            layerPatch.Velocity = basePatch.Velocity;

            var leaf = basePatch.GetLeafSynthPatch();
            if (leaf == null)
                throw new InvalidOperationException("basePatch does not contain a leaf SynthPatch.");

            for (var j = 0; j < leaf.Effects?.Count; j++)
                layerPatch.Effects.Items.Add(leaf.Effects[j].Clone());

            // Optional: Validate Envelope
            bool hasEnvelope = false;
            for (int k = 0; k < layerPatch.Effects.Items.Count; k++)
            {
                if (layerPatch.Effects.Items[k] is EnvelopeEffect)
                {
                    hasEnvelope = true;
                    break;
                }
            }
            if (!hasEnvelope)
                throw new InvalidOperationException("PowerChordPatch requires the base patch to include an EnvelopeEffect.");

            outVoices.Add(SynthSignalSource.Create(freq, layerPatch, master, nestedKnob));
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        basePatch = null;
        intervals = null;
        detuneCents = 0f;
        panSpread = 0f;
    }
}
