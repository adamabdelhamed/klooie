using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static klooie.Animator;

namespace klooie;
public static partial  class Animator
{
    private class FloatAnimationState : CommonAnimationState
    {
        // Options
        public float From { get; set; }
        public float To { get; set; }
        public Action<float> Setter { get; set; }

        // Non-Options State
        public float OriginalFrom { get; set; }
        public float OriginalTo { get; set; }
        public virtual void Set(float percentage) => Setter(percentage);
        protected FloatAnimationState() { }
        private static LazyPool<FloatAnimationState> pool = new LazyPool<FloatAnimationState>(() => new FloatAnimationState());
        public static FloatAnimationState Create(float from, float to, double duration, Action<float> setter, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime , int targetFramesPerSecond )
        {
            var ret = pool.Value.Rent();
            ret.Construct(from, to, duration,setter, easingFunction, delayProvider , autoReverse , autoReverseDelay, loop , animationLifetime, targetFramesPerSecond);
            return ret;
        }
 
        protected void Construct(float from, float to, double duration, Action<float> setter, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime, int targetFramesPerSecond)
        {
            AddDependency(this);
            base.Construct(duration, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime, targetFramesPerSecond);
            From = from;
            To = to;
            OriginalFrom = from;
            OriginalTo = to;
            Setter = setter;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            OriginalFrom = 0;
            OriginalTo = 0;
            From = 0;
            To = 0;
            Setter = null;
        }

        // --- Animation sequence helpers ---
        public static void StartForwardAnimation(object stateObj) => StartForwardAnimation((FloatAnimationState)stateObj);
        public static ILifetime StartForwardAnimation(FloatAnimationState state)
        {
            Animator.AnimateInternal(state, AfterForward);
            return state;
        }

        private static void AfterForward(FloatAnimationState state)
        {
            if (state.AutoReverse)
            {
                if (state.AutoReverseDelay > 0)
                {
                    ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.AutoReverseDelay, state, StartReverse);
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

        private static void StartReverse(FloatAnimationState state)
        {
            var temp = state.From;
            state.From = state.To;
            state.To = temp;
            AnimateInternal(state, AfterReverse);
        }

        private static void AfterReverse(FloatAnimationState state)
        {
            if (state.AutoReverseDelay > 0)
            {
                ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.AutoReverseDelay, state, FinishReverse);
            }
            else
            {
                FinishReverse(state);
            }
        }

        private static void FinishReverse(FloatAnimationState state)
        {
            state.From = state.OriginalFrom;
            state.To = state.OriginalTo;
            CompleteOrLoop(state);
        }

        private static void CompleteOrLoop(FloatAnimationState state)
        {
            if (state.LoopShouldContinue)
            {
                StartForwardAnimation(state);
            }
            else
            {
                state.From = state.OriginalFrom;
                state.To = state.OriginalTo;
                state.Tcs?.SetResult();
                state.Dispose();
            }
        }
    }

    private class FloatAnimationState<T> : FloatAnimationState
    {
        /// <summary>
        /// The action that applies the current animation value when it is time
        /// </summary>
        public Action<T, float> Setter { get; set; }
        /// <summary>
        /// The object that the setter will be called on
        /// </summary>
        public T Target { get; set; }

        public override void Set(float percentage) => Setter(Target, percentage);
        private static LazyPool<FloatAnimationState<T>> pool = new LazyPool<FloatAnimationState<T>>(() => new FloatAnimationState<T>());
        public static FloatAnimationState<T> Create(float from, float to, double duration, T target, Action<T, float> setter, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime loop, ILifetime? animationLifetime, int targetFramesPerSecond)
        {
            var ret = pool.Value.Rent();
            ret.Construct(from, to, duration, target, setter, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime, targetFramesPerSecond);
            return ret;
        }

        protected void Construct(float from, float to, double duration, T target, Action<T, float> setter, EasingFunction easingFunction, IDelayProvider delayProvider, bool autoReverse, float autoReverseDelay, ILifetime? loop, ILifetime? animationLifetime, int targetFramesPerSecond)
        {
            base.Construct(from, to, duration, null, easingFunction, delayProvider, autoReverse, autoReverseDelay, loop, animationLifetime, targetFramesPerSecond);
            Target = target;
            Setter = setter;
        }
    }
}