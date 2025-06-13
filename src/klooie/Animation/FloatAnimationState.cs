using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static partial  class Animator
{

    public class FloatAnimationState : DelayState
    {
        public FloatAnimationOptions Options { get; set; }
        public float OriginalFrom { get; set; }
        public float OriginalTo { get; set; }
        public int LoopLease { get; set; }
        public TaskCompletionSource? Tcs { get; set; }

        private FloatAnimationState() { }
        private static LazyPool<FloatAnimationState> pool = new LazyPool<FloatAnimationState>(() => new FloatAnimationState());
        public static FloatAnimationState Create()
        {
            var ret = pool.Value.Rent();
            ret.AddDependency(ret);
            return ret;
        }

        protected override void OnInit()
        {
            base.OnInit();
            Options = null;
            OriginalFrom = 0;
            OriginalTo = 0;
            LoopLease = 0;
            Tcs = null;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Options = null;
            OriginalFrom = 0;
            OriginalTo = 0;
            LoopLease = 0;
            Tcs = null;
        }

        // --- Animation sequence helpers ---
        public static void StartForwardAnimation(object stateObj) => StartForwardAnimation((FloatAnimationState)stateObj);
        public static void StartForwardAnimation(FloatAnimationState state)
        {
            Animator.AnimateInternal(state.Options, state, AfterForward);
        }

        private static void AfterForward(object o)
        {
            var state = (FloatAnimationState)o;
            if (state.Options.AutoReverse)
            {
                if (state.Options.AutoReverseDelay > 0)
                {
                    ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.Options.AutoReverseDelay, state, StartReverse);
                }
                else
                {
                    StartReverse(state);
                }
            }
            else
            {
                CompleteOrLoop(state);
            }
        }

        private static void StartReverse(object o)
        {
            var state = (FloatAnimationState)o;
            var temp = state.Options.From;
            state.Options.From = state.Options.To;
            state.Options.To = temp;
            AnimateInternal(state.Options, state, AfterReverse);
        }

        private static void AfterReverse(object o)
        {
            var state = (FloatAnimationState)o;
            if (state.Options.AutoReverseDelay > 0)
            {
                ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.Options.AutoReverseDelay, state, FinishReverse);
            }
            else
            {
                FinishReverse(state);
            }
        }

        private static void FinishReverse(object o)
        {
            var state = (FloatAnimationState)o;
            state.Options.From = state.OriginalFrom;
            state.Options.To = state.OriginalTo;
            CompleteOrLoop(state);
        }

        private static void CompleteOrLoop(FloatAnimationState state)
        {
            if (state.Options.Loop != null && state.Options.Loop.IsStillValid(state.LoopLease))
            {
                StartForwardAnimation(state);
            }
            else
            {
                state.Options.From = state.OriginalFrom;
                state.Options.To = state.OriginalTo;
                state.Tcs?.SetResult();
                state.Dispose();
            }
        }
    }
}