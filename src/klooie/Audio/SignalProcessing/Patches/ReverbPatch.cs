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

        wet.ForEachLeafPatch(leaf =>
        {
            if (leaf is SynthPatch wetLeaf == false) return;
            ReplaceEnvelopeWithLongerRelease(wetLeaf);
            wetLeaf.Effects.Items.Add(ReverbEffect.Create(s.EffectSettings));
        });
     
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

    public IEnumerable<ISynthPatch> GetPatches()
    {
        yield return dry;
        yield return wet;
    }

    public IEnumerable<SynthSignalSource> SpawnVoices(float frequencyHz, VolumeKnob master, ScheduledNoteEvent noteEvent)
    {
        foreach(var voice in dry.SpawnVoices(frequencyHz, master, noteEvent))
        {
            yield return voice;
        }
        foreach(var voice in wet.SpawnVoices(frequencyHz, master, noteEvent))
        {
            yield return voice;
        }
    }

    protected override void OnReturn()
    {
        ((Recyclable)dry)?.Dispose();
        dry = null!;
        ((Recyclable)wet)?.Dispose();
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
