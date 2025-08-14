using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static klooie.SynthSignalSource;

namespace klooie;
public interface ICompositePatch : ISynthPatch 
{
    IEnumerable<ISynthPatch> GetPatches();
}

public interface ISynthPatch
{
    bool IsNotePlayable(int midiNote) => true;  // default: always playable
    IEnumerable<SynthSignalSource> SpawnVoices(float frequencyHz, ScheduledNoteEvent noteEvent);
    ISynthPatch Clone();
}

[SynthCategory("Core")]
[SynthDocumentation("""
Single-oscillator patch that forms the basis of most instruments.  Optional
components include a sub oscillator, vibrato, gentle pitch drift and a
pluggable effects chain.  Use this as a starting point for custom patches.
""")]
public class SynthPatch : Recyclable, ISynthPatch
{
    public ISynthPatch InnerPatch => this;
    private SynthPatch() { }
    private static LazyPool<SynthPatch> _pool = new(() => new SynthPatch());
    public static SynthPatch Create(NoteExpression note)
    {
        var patch = _pool.Value.Rent();
        patch.Waveform = WaveformType.Sine; 
        patch.Note = note;
        patch.EnableTransient = false;
        patch.EnableSubOsc = false;
        patch.EnablePitchDrift = false; 
        patch.Velocity = 127;
        patch.FrequencyOverride = null;
        return patch;
    }


    public ModParamSpec PitchCents = 0f;
    public ModParamSpec Pan = new ModParamSpec().WithClamp(-1f, +1f).WithSmoothing(0.2f);


    public NoteExpression Note { get; private set; }


    public WaveformType Waveform { get; set; }
    public bool EnableTransient { get; set; }
    public float TransientDurationSeconds { get; set; }

    public bool EnableSubOsc { get; set; }
    public float SubOscLevel { get; set; }  
    public int SubOscOctaveOffset { get; set; }  


    public bool EnablePitchDrift { get; set; }
    public float DriftFrequencyHz { get; set; }  
    public float DriftAmountCents { get; set; }    
    public int Velocity { get; set; }


    public float? FrequencyOverride { get; set; }

    public RecyclableList<IEffect> Effects { get; set; }

    protected override void OnInit()
    {
        base.OnInit();
        Effects = RecyclableListPool<IEffect>.Instance.Rent(20);
        Waveform = WaveformType.Sine;
        EnableTransient = false;
        TransientDurationSeconds = 0f;
        EnableSubOsc = false;
        SubOscLevel = 0f;
        SubOscOctaveOffset = 0;
        EnablePitchDrift = false;
        DriftFrequencyHz = 0f;
        DriftAmountCents = 0f;
        Velocity = 127;
        FrequencyOverride = null;
        PitchCents = 0f;
        Pan = new ModParamSpec().WithClamp(-1f, +1f).WithSmoothing(0.2f);
    }

    public ISynthPatch Clone()
    {
        var clone = SynthPatch.Create(this.Note);
        clone.Waveform = this.Waveform;
        clone.DriftFrequencyHz = this.DriftFrequencyHz;
        clone.DriftAmountCents = this.DriftAmountCents;
        clone.EnablePitchDrift = this.EnablePitchDrift;
        clone.EnableSubOsc = this.EnableSubOsc;
        clone.SubOscOctaveOffset = this.SubOscOctaveOffset;
        clone.SubOscLevel = this.SubOscLevel;
        clone.EnableTransient = this.EnableTransient;
        clone.TransientDurationSeconds = this.TransientDurationSeconds;
        clone.Velocity = this.Velocity;
        clone.FrequencyOverride = this.FrequencyOverride;
        clone.PitchCents = this.PitchCents;
        clone.Pan = this.Pan;
        // ...copy other fields as needed

        // Clone all effects (deep clone)
        if (this.Effects != null)
        {
            for (int i = 0; i < this.Effects.Count; i++)
                clone.Effects.Items.Add(this.Effects[i].Clone());
        }
        return clone;
    }

    public SynthPatch SetPitchCents(ModParamSpec spec) { PitchCents = spec; return this; }
    public SynthPatch SetPan(ModParamSpec spec) { Pan = spec; return this; }

    protected override void OnReturn()
    {
        base.OnReturn();
        for (var i = 0; i < Effects?.Count; i++)
        {
            if (Effects[i] is Recyclable r)
            {
               r.Dispose();
            }
        }
        Effects?.Dispose();
        Effects = null!;
        Note = null!;
    }

    public virtual IEnumerable<SynthSignalSource> SpawnVoices(float frequencyHz, ScheduledNoteEvent noteEvent)
    {
        var innerVoice = SynthSignalSource.Create(frequencyHz, this, noteEvent);
        yield return innerVoice;
    }
}


public interface IEffect
{
    // Process a mono sample (or stereo, if you want!)
    float Process(in EffectContext ctx);
    IEffect Clone();
}

