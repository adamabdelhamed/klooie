using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

[SynthCategory("Complex Patch")]
[SynthDocumentation("""
Generates several slightly detuned copies of a patch.  Each voice can be panned
across the stereo field to create a wide, lush sound.
""")]
public class UnisonPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch basePatch;
    private int numVoices;
    private float detuneCents;
    private float panSpread;

    private static LazyPool<UnisonPatch> _pool = new(() => new UnisonPatch());

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

    public ISynthPatch Clone() => UnisonPatch.Create(new Settings
    {
        BasePatch = this.basePatch.Clone(),
        NumVoices = this.numVoices,
        DetuneCents = this.detuneCents,
        PanSpread = this.panSpread,
    });

    protected void Construct(Settings settings)
    {
        this.basePatch = settings.BasePatch ?? throw new ArgumentNullException(nameof(settings.BasePatch));
        this.numVoices = settings.NumVoices;
        this.detuneCents = settings.DetuneCents;
        this.panSpread = settings.PanSpread;

        _innerPatches = new ISynthPatch[numVoices];
        for (int i = 0; i < numVoices; i++)
        {
            var nestedPatch = basePatch.Clone();
            _innerPatches[i] = nestedPatch;
        }
    }

    public void SpawnVoices(
        float frequencyHz,
        VolumeKnob master,
        ScheduledNoteEvent noteEvent,
        List<SynthSignalSource> outVoices)
    {
        for (int i = 0; i < numVoices; i++)
        {
            float rel = (i - (numVoices - 1) / 2.0f);
            float detune = rel * detuneCents / Math.Max(numVoices - 1, 1);
            float pan = rel * panSpread / Math.Max(numVoices - 1, 1);
            float detunedFreq = frequencyHz * MathF.Pow(2f, detune / 1200f);

            var leaves = RecyclableListPool<ISynthPatch>.Instance.Rent(8);
            try
            {
                _innerPatches[i].GetAllLeafPatches(leaves);
                foreach (var leaf in leaves.Items)
                {
                    if (leaf is SynthPatch synthLeaf)
                    {
                        outVoices.Add(SynthSignalSource.Create(detunedFreq, synthLeaf, master, noteEvent));
                    }
                }
            }
            finally
            {
                leaves.Dispose();
            }
  
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

    [SynthDocumentation("""
Settings describing the base patch and how many detuned voices to create
along with their pan spread.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Patch that will be duplicated for every voice.
""")]
        public ISynthPatch BasePatch;

        [SynthDocumentation("""
How many detuned voices to spawn.
""")]
        public int NumVoices;

        [SynthDocumentation("""
Total amount of detuning across all voices in
cents.
""")]
        public float DetuneCents;

        [SynthDocumentation("""
Stereo spread of the voices where -1 is hard left
and +1 is hard right.
""")]
        public float PanSpread;
    }
}
