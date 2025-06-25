using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static klooie.Animator;

namespace klooie;
public static partial class Animator
{
    private class AnimationFrameState : DelayState
    {
        public FloatAnimationState AnimationState { get; set; }
        public float NumberOfFrames { get; set; }
        public float InitialValue { get; set; }
        public float Delta { get; set; }
        public long StartTime { get; set; }
        public float I { get; set; }

        private AnimationFrameState() { }
        private static LazyPool<AnimationFrameState> pool = new LazyPool<AnimationFrameState>(() => new AnimationFrameState());
        public static AnimationFrameState Create()
        {
            var ret = pool.Value.Rent();
            ret.AddDependency(ret);
            return ret;
        }

        protected override void OnInit()
        {
            base.OnInit();
            AnimationState = null;
            NumberOfFrames = 0;
            InitialValue = 0;
            Delta = 0;
            StartTime = 0;
            I = 0;
        }
    }
}