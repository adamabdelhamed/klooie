using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace klooie;

[SynthCategory("Complex Patch")]
[SynthDocumentation("""
Creates power chords by layering transposed copies of a base patch.  Each layer
can be slightly detuned and panned for a wide stereo effect.
""")]
public class PowerChordPatch : Recyclable, ISynthPatch, ICompositePatch
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

    private ISynthPatch[] patches;
    public void GetPatches(List<ISynthPatch> patches)
    {
        patches.AddRange(this.patches);
    }

    private PowerChordPatch() { }

    public static PowerChordPatch Create(Settings settings)
    {
        var patch = _pool.Value.Rent();
        patch.Construct(settings);
        return patch;
    }

    protected void Construct(Settings settings)
    {
        this.basePatch = settings.BasePatch ?? throw new ArgumentNullException(nameof(settings.BasePatch));
        this.intervals = settings.Intervals ?? new int[] { 0, 7 }; // Default: power chord (root + 5th)
        this.detuneCents = settings.DetuneCents;
        this.panSpread = settings.PanSpread;

        int numLayers = intervals.Length;
        patches = new ISynthPatch[numLayers];
        for (int i = 0; i < numLayers; i++)
        {
            int interval = intervals[i];
            float rel = (i - (numLayers - 1) / 2.0f);

            // Calculate detune and pan for each layer
            float detune = rel * detuneCents / Math.Max(numLayers - 1, 1);
            float pan = rel * panSpread / Math.Max(numLayers - 1, 1);

            // Clone basePatch for each layer (deep copy of effects)
            var layerPatch = SynthPatch.Create();
            patches[i] = layerPatch;
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
        }
    }

    public void SpawnVoices(float frequencyHz, VolumeKnob master, NoteExpression note, List<SynthSignalSource> outVoices)
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

            outVoices.Add(SynthSignalSource.Create(freq, (SynthPatch)patches[i], master, note));
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        basePatch = null;
        intervals = null;
        detuneCents = 0f;
        panSpread = 0f;
        patches = null;
    }

    [SynthDocumentation("""
Settings describing which patch to copy for each chord tone along with
detune and stereo spread options.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
The base patch that will be duplicated for each
chord note.
""")]
        public ISynthPatch BasePatch;

        [SynthDocumentation("""
Array of semitone offsets defining the chord
intervals.
""")]
        public int[]? Intervals;

        [SynthDocumentation("""
Total detune range applied across the layers in
cents.
""")]
        public float DetuneCents;

        [SynthDocumentation("""
Stereo spread of the layers where -1 is far left
and +1 is far right.
""")]
        public float PanSpread;
    }
}
