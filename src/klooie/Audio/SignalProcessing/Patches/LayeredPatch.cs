using klooie;

[SynthCategory("Complex Patch")]
[SynthDocumentation("""
Allows several patches to be played together as layers.  Each layer
has its own volume, stereo pan and pitch offset so you can build rich,
multi-voice instruments.
""")]
public sealed class LayeredPatch : Recyclable, ISynthPatch, ICompositePatch
{
    private ISynthPatch[] layers;
    private float[] layerVolumes;
    private float[] layerPans;
    private int[] layerTransposes;
    public ISynthPatch[] Layers => layers;
    public float[] LayerVolumes => layerVolumes;
    public float[] LayerPans => layerPans;
    public int[] LayerTransposes => layerTransposes;

    private static readonly LazyPool<LayeredPatch> _pool = new(() => new LayeredPatch());

    private LayeredPatch() { }

    public static LayeredPatch Create(Settings settings)
    {
        var p = _pool.Value.Rent();
        int count = settings.Patches.Length;

        p.layers = settings.Patches;
        p.layerVolumes = ValidateOrCreate(settings.Volumes, count, 1f);
        p.layerPans = ValidateOrCreate(settings.Pans, count, 0f);
        p.layerTransposes = ValidateOrCreate(settings.Transposes, count, 0);

        return p;
    }

    public ISynthPatch Clone() => LayeredPatch.Create(new Settings
    {
        Patches = this.layers.Select(p => p.Clone()).ToArray(),
        Volumes = (float[])this.LayerVolumes.Clone(),
        Pans = (float[])this.LayerPans.Clone(),
        Transposes = (int[])this.LayerTransposes.Clone()
    });


    private static float[] ValidateOrCreate(float[]? arr, int count, float def)
    {
        if (arr != null && arr.Length == count) return arr;
        var a = new float[count];
        for (int i = 0; i < count; i++) a[i] = def;
        return a;
    }
    private static int[] ValidateOrCreate(int[]? arr, int count, int def)
    {
        if (arr != null && arr.Length == count) return arr;
        var a = new int[count];
        for (int i = 0; i < count; i++) a[i] = def;
        return a;
    }

    public IEnumerable<ISynthPatch> GetPatches() => layers;

    public bool IsNotePlayable(int midiNote)
    {
        foreach (var l in layers)
            if (l?.IsNotePlayable(midiNote) ?? true)
                return true;
        return false;
    }

    public IEnumerable<SynthSignalSource> SpawnVoices(
        float freq,
        VolumeKnob master,
        ScheduledNoteEvent noteEvent)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            float transFreq = (layerTransposes[i] == 0)
                ? freq
                : freq * MathF.Pow(2f, layerTransposes[i] / 12f);

            foreach (var voice in layers[i].SpawnVoices(transFreq, master, noteEvent))
            {
                yield return voice;
            }
        }
    }

    protected override void OnReturn()
    {
        if (layers != null)
            foreach (var l in layers)
                if (l is Recyclable r) r.Dispose();

        layers = null!;
        layerVolumes = null!;
        layerPans = null!;
        layerTransposes = null!;
        base.OnReturn();
    }

    [SynthDocumentation("""
Configuration describing which patches are layered together and
how each layer is mixed in terms of volume, pan position and
transpose amount.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Array of patches that will play at the same time.  The
index of each patch matches the entries in Volumes, Pans and Transposes.
""")]
        public ISynthPatch[] Patches;

        [SynthDocumentation("""
Relative volume of each layer from 0 to 1.  Values
above 1 will boost that layer while values below 1 reduce it.
""")]
        public float[]? Volumes;

        [SynthDocumentation("""
Stereo pan for each layer where -1 is full left and
+1 is full right.
""")]
        public float[]? Pans;

        [SynthDocumentation("""
Number of semitones each layer is transposed
relative to the original pitch.
""")]
        public int[]? Transposes;
    }

    public static LayeredPatchBuilder CreateBuilder()
       => new LayeredPatchBuilder();
}


public sealed class LayeredPatchBuilder
{
    private readonly List<ISynthPatch> _patches = new();
    private readonly List<float> _volumes = new();
    private readonly List<float> _pans = new();
    private readonly List<int> _transposes = new();

    /// <summary>
    /// Adds a layer with configurable mixing and transposition.
    /// </summary>
    public LayeredPatchBuilder AddLayer(
        float volume = 1f,
        float pan = 0f,
        int transpose = 0,
        ISynthPatch patch = null)
    {
        if (patch == null)
            throw new ArgumentNullException(nameof(patch), "Layer patch cannot be null");
        _patches.Add(patch);
        _volumes.Add(volume);
        _pans.Add(pan);
        _transposes.Add(transpose);
        return this;
    }

    /// <summary>
    /// Finalizes and builds the LayeredPatch.
    /// </summary>
    public LayeredPatch Build()
    {
        if (_patches.Count == 0)
            throw new InvalidOperationException("At least one layer must be added.");

        return LayeredPatch.Create(new LayeredPatch.Settings
        {
            Patches = _patches.ToArray(),
            Volumes = _volumes.ToArray(),
            Pans = _pans.ToArray(),
            Transposes = _transposes.ToArray()
        });
    }
}
