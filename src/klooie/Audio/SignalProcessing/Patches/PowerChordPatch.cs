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
    private int[] intervals;
    private float detuneCents;
    private float panSpread;

    private static LazyPool<PowerChordPatch> _pool = new(() => new PowerChordPatch());

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

    public ISynthPatch Clone() => PowerChordPatch.Create(new Settings
    {
        BasePatch = this.basePatch.Clone(),
        Intervals = this.intervals,
        DetuneCents = this.detuneCents,
        PanSpread = this.panSpread,
    });

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
            var layerPatch = basePatch.Clone();
            patches[i] = layerPatch; }
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
