using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

[SynthCategory("Utility")]
[SynthDescription("""
Creates multiple detuned copies of a patch for wide, thick sounds
with controllable pan spread.
""")]
public class UnisonPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch basePatch;
    public ISynthPatch InnerPatch => basePatch;
    private int numVoices;
    private float detuneCents;
    private float panSpread;

    private static LazyPool<UnisonPatch> _pool = new(() => new UnisonPatch());

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

    private ISynthPatch[] _innerPatches;
    public void GetPatches(List<ISynthPatch> patches)
    {
        patches.AddRange(_innerPatches);
    }

    private UnisonPatch() { }

    public static UnisonPatch Create(Settings settings)
    {
        var patch = _pool.Value.Rent();
        patch.Construct(settings);
        return patch;
    }

    protected void Construct(Settings settings)
    {
        this.basePatch = settings.BasePatch ?? throw new ArgumentNullException(nameof(settings.BasePatch));
        this.numVoices = settings.NumVoices;
        this.detuneCents = settings.DetuneCents;
        this.panSpread = settings.PanSpread;

        _innerPatches = new ISynthPatch[numVoices];
        for (int i = 0; i < numVoices; i++)
        {
            var nestedPatch = SynthPatch.Create();
            _innerPatches[i] = nestedPatch;
            nestedPatch.Waveform = basePatch.Waveform;
            nestedPatch.DriftFrequencyHz = basePatch.DriftFrequencyHz;
            nestedPatch.DriftAmountCents = basePatch.DriftAmountCents;
            nestedPatch.EnablePitchDrift = basePatch.EnablePitchDrift;
            nestedPatch.EnableSubOsc = basePatch.EnableSubOsc;
            nestedPatch.SubOscOctaveOffset = basePatch.SubOscOctaveOffset;
            nestedPatch.SubOscLevel = basePatch.SubOscLevel;
            nestedPatch.EnableTransient = basePatch.EnableTransient;
            nestedPatch.TransientDurationSeconds = basePatch.TransientDurationSeconds;
            nestedPatch.Velocity = basePatch.Velocity;

            var leaf = basePatch.GetLeafSynthPatch();
            if (leaf == null)
                throw new InvalidOperationException("UnisonPatch basePatch does not contain a leaf SynthPatch.");

            for (var j = 0; j < leaf.Effects?.Count; j++)
                nestedPatch.Effects.Items.Add(leaf.Effects[j].Clone());

            // Optional: Validate Envelope
            bool hasEnvelope = false;
            for (int k = 0; k < nestedPatch.Effects.Items.Count; k++)
            {
                if (nestedPatch.Effects.Items[k] is EnvelopeEffect)
                {
                    hasEnvelope = true;
                    break;
                }
            }
            if (!hasEnvelope)
                throw new InvalidOperationException("UnisonPatch requires the base patch to include an EnvelopeEffect.");
        }
    }

    public void SpawnVoices(
        float frequencyHz,
        VolumeKnob master,
        NoteExpression note,
        List<SynthSignalSource> outVoices)
    {
        for (int i = 0; i < numVoices; i++)
        {
            float rel = (i - (numVoices - 1) / 2.0f);
            float detune = rel * detuneCents / Math.Max(numVoices - 1, 1);
            float pan = rel * panSpread / Math.Max(numVoices - 1, 1);
            float detunedFreq = frequencyHz * MathF.Pow(2f, detune / 1200f);

            outVoices.Add(SynthSignalSource.Create(detunedFreq, (SynthPatch)_innerPatches[i], master, note));
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        basePatch = null;
        numVoices = 0;
        detuneCents = 0f;
        panSpread = 0f;
        _innerPatches = null!;
    }

    [SynthDescription("""
    Parameters for creating a unison patch.
    """)]
    public struct Settings
    {
        [SynthDescription("""Patch to clone for each voice.""")]
        public ISynthPatch BasePatch;

        [SynthDescription("""Number of detuned voices.""")]
        public int NumVoices;

        [SynthDescription("""Total detune range in cents.""")]
        public float DetuneCents;

        [SynthDescription("""Stereo spread of the voices (-1 to 1).""")]
        public float PanSpread;
    }
}
