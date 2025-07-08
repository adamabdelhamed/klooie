using System;
using System.Collections.Generic;
using System.Drawing.Imaging.Effects;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class UnisonPatch : Recyclable, ISynthPatch
{
    private ISynthPatch basePatch;
    private int numVoices;
    private float detuneCents;
    private float panSpread;

    private static LazyPool<UnisonPatch> _pool = new(() => new UnisonPatch());

    public ADSREnvelope Envelope => basePatch.Envelope;

    public WaveformType Waveform => basePatch.Waveform;

    public float DriftFrequencyHz => basePatch.DriftFrequencyHz;

    public float DriftAmountCents => basePatch.DriftAmountCents;

    public bool EnablePitchDrift =>  basePatch.EnablePitchDrift;

    public bool EnableSubOsc => basePatch.EnableSubOsc;

    public int SubOscOctaveOffset => basePatch.SubOscOctaveOffset;

    public float SubOscLevel => basePatch.SubOscLevel;

    public bool EnableLowPassFilter => basePatch.EnableLowPassFilter;

    public float FilterAlpha => basePatch.FilterAlpha;

    public bool EnableDynamicFilter => basePatch.EnableDynamicFilter;

    public float FilterBaseAlpha => basePatch.FilterBaseAlpha;

    public float FilterMaxAlpha => basePatch.FilterMaxAlpha;

    public bool EnableTransient => basePatch.EnableTransient;

    public float TransientDurationSeconds => basePatch.TransientDurationSeconds;

    public float Velocity => basePatch.Velocity;

    private List<IEffect> effects;
    public List<IEffect>? Effects => effects;

    private UnisonPatch() { }

    public static UnisonPatch Create(
        ISynthPatch basePatch,
        int numVoices = 2,
        float detuneCents = 6f,
        float panSpread = 0.8f)
    {
        var patch = _pool.Value.Rent();
        patch.Construct(basePatch, numVoices, detuneCents, panSpread);
        return patch;
    }

    protected void Construct(ISynthPatch basePatch, int numVoices = 2, float detuneCents = 6f, float panSpread = 0.8f)
    {
        this.basePatch = basePatch;
        this.numVoices = numVoices;
        this.detuneCents = detuneCents;
        this.panSpread = panSpread;

    }

    public void SpawnVoices(
        float frequencyHz,
        VolumeKnob master,
        VolumeKnob? sampleKnob,
        List<SynthSignalSource> outVoices)
    {
        for (int i = 0; i < numVoices; i++)
        {
            float rel = (i - (numVoices - 1) / 2.0f);
            float detune = rel * detuneCents / Math.Max(numVoices - 1, 1);
            float pan = rel * panSpread / Math.Max(numVoices - 1, 1);
            float detunedFreq = frequencyHz * MathF.Pow(2f, detune / 1200f);

            // Use a cloned knob for per-voice pan
            var nestedKnob = sampleKnob != null ? VolumeKnob.Create() : null;
            if(nestedKnob != null)
            {
                OnDisposed(nestedKnob, Recyclable.TryDisposeMe);
                nestedKnob.Volume = sampleKnob.Volume; // Copy the volume from the master knob
                nestedKnob.Pan = sampleKnob.Pan; // Copy the pan from the master knob
                sampleKnob.VolumeChanged.Subscribe(sampleKnob, static (me, v) => me.Volume = v, nestedKnob);
                sampleKnob.PanChanged.Subscribe(nestedKnob, static (me, v) => me.Pan = v, nestedKnob);
            }
            
            nestedKnob.Pan = pan;

            var nestedPatch = SynthPatch.Create();
            nestedPatch.Envelope.Attack = basePatch.Envelope.Attack;
            nestedPatch.Envelope.Decay = basePatch.Envelope.Decay;
            nestedPatch.Envelope.Sustain = basePatch.Envelope.Sustain;
            nestedPatch.Envelope.Release = basePatch.Envelope.Release;
            nestedPatch.Waveform = basePatch.Waveform;
            nestedPatch.DriftFrequencyHz = basePatch.DriftFrequencyHz;
            nestedPatch.DriftAmountCents = basePatch.DriftAmountCents;
            nestedPatch.EnablePitchDrift = basePatch.EnablePitchDrift;
            nestedPatch.EnableSubOsc = basePatch.EnableSubOsc;
            nestedPatch.SubOscOctaveOffset = basePatch.SubOscOctaveOffset;
            nestedPatch.SubOscLevel = basePatch.SubOscLevel;
            nestedPatch.EnableLowPassFilter = basePatch.EnableLowPassFilter;
            nestedPatch.FilterAlpha = basePatch.FilterAlpha;
            nestedPatch.EnableDynamicFilter = basePatch.EnableDynamicFilter;
            nestedPatch.FilterBaseAlpha = basePatch.FilterBaseAlpha;
            nestedPatch.FilterMaxAlpha = basePatch.FilterMaxAlpha;
            nestedPatch.EnableTransient = basePatch.EnableTransient;
            nestedPatch.EnableTransient = nestedPatch.EnableTransient;
            nestedPatch.TransientDurationSeconds = basePatch.TransientDurationSeconds;
            nestedPatch.Velocity = basePatch.Velocity;

            outVoices.Add(SynthSignalSource.Create(detunedFreq, nestedPatch, master, nestedKnob));
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        basePatch = null;
        numVoices = 0;
        detuneCents = 0f;
        panSpread = 0f;
    }
}
