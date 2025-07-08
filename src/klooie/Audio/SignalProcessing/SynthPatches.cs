using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class SynthPatches
{
    public static SynthPatch CreateGuitar()
    => SynthPatch.Create()
        .WithLowPass(0.02f)
        .WithEnvelope(.005f, 0.1f, 0.25f, 0.4f);


    public static SynthPatch CreateBass()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithSubOscillator(.06f, - 1)
        .WithLowPass(.01f)
        .WithDistortion()
        .WithEnvelope(0.005f, 0.12f, 0.5f, 0.4f);
    public static SynthPatch CreateAnalogPad()
     => SynthPatch.Create()
         .WithWaveForm(WaveformType.Saw)
         .WithPitchDrift(0.3f, 7f)
         .WithSubOscillator(0.5f, -1)
         .WithChorus(delayMs: 22, depthMs: 7, rateHz: 0.22f, mix: 0.19f)
         .WithReverb(feedback: 0.73f, diffusion: 0.52f, wet: 0.26f, dry: 0.74f)
         .WithVolume(.1f)
         .WithEffect(EnvelopeEffect.Create(0.23f, 1.3f, 0.85f, 1.6f));

    public static SynthPatch CreateLead()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Saw)
        .WithPitchDrift(0.2f, 3f)
        .WithDistortion()
        .WithLowPass(0.03f)
        .WithChorus()
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0.01f, 0.15f, 0.6f, 0.25f));

    public static SynthPatch CreateKick()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Sine)
        .WithTransient(0.005f)
        .WithDistortion()
        .WithHighPass(20f)
        .WithEffect(EnvelopeEffect.Create(0, 0.1f, 0.0f, 0.1f));

    public static SynthPatch CreateSnare()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Noise)
        .WithTransient(0.005f)
        .WithDistortion()
        .WithLowPass(0.2f)
        .WithHighPass(1000f)
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0, .1, 0, 0.2f));

    public static SynthPatch CreateRhythmicPad()
    => SynthPatch.Create()
        .WithWaveForm(WaveformType.Saw)
        .WithPitchDrift(0.15f, 5f)
        .WithSubOscillator(0.5f, -1)
        .WithTremolo(0.5f, 5f)
        .WithLowPass(0.015f)
        .WithReverb()
        .WithEffect(EnvelopeEffect.Create(0.5f, 1.0f, 0.75f, 1.5f));

    public static ISynthPatch CreateRockGuitar()
            => AmpedRockGuitarPatch.Create();
}