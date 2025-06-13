using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AnimationState : DelayState
{
    public FloatAnimationOptions Options { get; set; }
    public float NumberOfFrames { get; set; }
    public TimeSpan TimeBetweenFrames { get; set; }
    public float InitialValue { get; set; }
    public float Delta { get; set; }
    public long StartTime { get; set; }
    public float I { get; set; }

    private AnimationState() { }
    private static LazyPool<AnimationState> pool = new LazyPool<AnimationState>(() => new AnimationState());
    public static AnimationState Create()
    {
        var ret = pool.Value.Rent();
        ret.AddDependency(ret);
        return ret;
    }

    protected override void OnInit()
    {
        base.OnInit();
        Options = null;
        NumberOfFrames = 0;
        TimeBetweenFrames = TimeSpan.Zero;
        InitialValue = 0;
        Delta = 0;
        StartTime = 0;
        I = 0;
    }
}
