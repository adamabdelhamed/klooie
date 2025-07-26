using System;
using System.Collections.Generic;

namespace klooie;

[SynthCategory("Complex Patch")]
[SynthDocumentation("""
A patch that applies stereo reverb to the inner patch signal. 
Spawns two voices per note: 
  - The dry voice (with the original envelope and no reverb)
  - The wet voice (with the reverb applied and a longer envelope for a natural tail)
""")]
public class ReverbPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch dry;
    private ISynthPatch wet;
    private Settings patchSettings;
    private static LazyPool<ReverbPatch> _pool = new(() => new ReverbPatch());
    private ReverbPatch() { }
    public static ReverbPatch Create(Settings settings) => _pool.Value.Rent().Construct(settings);
    public ISynthPatch Clone() => ReverbPatch.Create(new Settings { BasePatch = this.dry.Clone(), EffectSettings = this.patchSettings.EffectSettings });

    protected ReverbPatch Construct(Settings s)
    {
        this.dry = s.BasePatch ?? throw new ArgumentNullException(nameof(s.BasePatch));
        this.wet = dry.Clone();
        this.patchSettings = s;
        var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(8);
        try
        {
            wet.GetAllLeafPatches(leaves);
            foreach (var leaf in leaves.Items)
            {
                if (leaf is SynthPatch wetLeaf == false) continue;
                ReplaceEnvelopeWithLongerRelease(wetLeaf);
                wetLeaf.Effects.Items.Add(ReverbEffect.Create(s.EffectSettings));
            }
        }
        finally
        {
            leaves.Dispose();
        }
        return this;
    }

    private void ReplaceEnvelopeWithLongerRelease(SynthPatch wet)
    {
        ADSREnvelope? orig = null;
        for (int i = 0; i < wet.Effects.Items.Count; i++)
        {
            if (wet.Effects[i] is EnvelopeEffect ee == false) continue;
            orig = ee.Envelope;
            wet.Effects.Items.RemoveAt(i);
            wet.Effects.Items.Insert(i, EnvelopeEffect.Create(orig.Attack, orig.Decay, orig.Sustain, patchSettings.Duration));
            break;
        }

        if (orig == null) throw new InvalidOperationException("ReverbPatch requires an EnvelopeEffect in the base patch.");
        if(wet.Effects.OfType<EnvelopeEffect>().Count() != 1) throw new InvalidOperationException("ReverbPatch requires a single EnvelopeEffect in the base patch.");
    }

    public void GetPatches(List<ISynthPatch> buffer)
    {
        buffer.Add(dry);
        buffer.Add(wet);
    }

    public void SpawnVoices(float frequencyHz, VolumeKnob master, ScheduledNoteEvent noteEvent, List<SynthSignalSource> outVoices)
    {
        dry.SpawnVoices(frequencyHz, master, noteEvent, outVoices);
        wet.SpawnVoices(frequencyHz, master, noteEvent, outVoices);
    }

    protected override void OnReturn()
    {
        (dry as Recyclable)?.Dispose();
        dry = null!;
        (wet as Recyclable)?.Dispose();
        wet = null!;
        patchSettings = default;
        base.OnReturn();
    }

    [SynthDocumentation("""
Settings for constructing a ReverbPatch. Mirrors ReverbEffect.Settings, plus the dry patch.
""")]
    public struct Settings
    {
        [SynthDocumentation("Patch that will be split into dry and wet branches.")]
        public ISynthPatch BasePatch;

        [SynthDocumentation("Inner Settings")]
        public ReverbEffect.Settings EffectSettings;

        [SynthDocumentation("""Duration of the reverb tail in seconds.""")]
        public float Duration;
    }
}
