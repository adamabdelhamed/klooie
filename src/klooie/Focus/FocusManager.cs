﻿namespace klooie;

public partial class ConsoleApp : EventLoop
{
    private class KeyboardInterceptionManager
    {
        private class HandlerContext
        {
            internal Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>> NakedHandlers { get; private set; } = new Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>>();
            internal Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>> AltHandlers { get; private set; } = new Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>>();
            internal Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>> ShiftHandlers { get; private set; } = new Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>>();
            internal Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>> ControlHandlers { get; private set; } = new Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>>();
        }

        private Stack<HandlerContext> handlerStack;

        internal KeyboardInterceptionManager()
        {
            handlerStack = new Stack<HandlerContext>();
            handlerStack.Push(new HandlerContext());
        }

        internal bool TryIntercept(ConsoleKeyInfo keyInfo)
        {
            bool alt = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt);
            bool control = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
            bool shift = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool noModifier = alt == false && shift == false && control == false;

            int handlerCount = 0;
            try
            {
                if (noModifier && handlerStack.Peek().NakedHandlers.ContainsKey(keyInfo.Key))
                {
                    handlerStack.Peek().NakedHandlers[keyInfo.Key].Peek().Invoke(keyInfo);
                    handlerCount++;
                }

                if (alt && handlerStack.Peek().AltHandlers.ContainsKey(keyInfo.Key))
                {
                    handlerStack.Peek().AltHandlers[keyInfo.Key].Peek().Invoke(keyInfo);
                    handlerCount++;
                }

                if (shift && handlerStack.Peek().ShiftHandlers.ContainsKey(keyInfo.Key))
                {
                    handlerStack.Peek().ShiftHandlers[keyInfo.Key].Peek().Invoke(keyInfo);
                    handlerCount++;
                }

                if (control && handlerStack.Peek().ControlHandlers.ContainsKey(keyInfo.Key))
                {
                    handlerStack.Peek().ControlHandlers[keyInfo.Key].Peek().Invoke(keyInfo);
                    handlerCount++;
                }

                return handlerCount > 0;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }


        internal ILifetime PushUnmanaged(ConsoleKey key, ConsoleModifiers? modifier, Action<ConsoleKeyInfo> handler)
        {
            Dictionary<ConsoleKey, Stack<Action<ConsoleKeyInfo>>> target;

            if (modifier.HasValue == false) target = handlerStack.Peek().NakedHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Alt)) target = handlerStack.Peek().AltHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Shift)) target = handlerStack.Peek().ShiftHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Control)) target = handlerStack.Peek().ControlHandlers;
            else throw new ArgumentException("Unsupported modifier: " + modifier.Value);

            Stack<Action<ConsoleKeyInfo>> targetStack;
            if (target.TryGetValue(key, out targetStack) == false)
            {
                targetStack = new Stack<Action<ConsoleKeyInfo>>();
                target.Add(key, targetStack);
            }

            targetStack.Push(handler);
            var lt = new Lifetime();
            lt.OnDisposed(() =>
            {
                targetStack.Pop();
                if (targetStack.Count == 0)
                {
                    target.Remove(key);
                }
            });

            return lt;
        }

        /// <summary>
        /// Pushes this handler onto its appropriate handler stack for the given lifetime
        /// </summary>
        /// <param name="key">the key ti handle</param>
        /// <param name="modifier">the modifier, or null if you want to handle the unmodified keypress</param>
        /// <param name="handler">the code to run when the key input is intercepted</param>
        /// <param name="manager">the lifetime of the handlers registration</param>
        public void PushForLifetime(ConsoleKey key, ConsoleModifiers? modifier, Action<ConsoleKeyInfo> handler, ILifetimeManager manager)
        {
            manager.OnDisposed(PushUnmanaged(key, modifier, handler));
        }

        /// <summary>
        /// Pushes this handler onto the appropriate handler stack
        /// </summary>
        /// <param name="key">the key ti handle</param>
        /// <param name="modifier">the modifier, or null if you want to handle the unmodified keypress</param>
        /// <param name="handler">the code to run when the key input is intercepted</param>
        /// <returns>A subscription that you should dispose when you no longer want this interception to happen</returns>
        public ILifetime PushUnmanaged(ConsoleKey key, ConsoleModifiers? modifier, Action handler)
        {
            return PushUnmanaged(key, modifier, (k) => { handler(); });
        }

        /// <summary>
        /// Pushes this handler onto its appropriate handler stack for the given lifetime
        /// </summary>
        /// <param name="key">the key ti handle</param>
        /// <param name="modifier">the modifier, or null if you want to handle the unmodified keypress</param>
        /// <param name="handler">the code to run when the key input is intercepted</param>
        /// <param name="manager">the lifetime of the handlers registration</param>
        public void PushForLifetime(ConsoleKey key, ConsoleModifiers? modifier, Action handler, ILifetimeManager manager)
        {
            PushForLifetime(key, modifier, (k) => { handler(); }, manager);
        }
    }

    private class FocusManager : ObservableObject
    {
        public class FocusContext
        {
            public KeyboardInterceptionManager Interceptors { get; private set; } = new KeyboardInterceptionManager();
            public List<ConsoleControl> Controls { get; internal set; }
            public int FocusIndex { get; internal set; }

            public FocusContext()
            {
                Controls = new List<ConsoleControl>();
                FocusIndex = -1;
            }
        }

        private Stack<FocusContext> focusStack;

        public Stack<FocusContext> Stack => focusStack;

        public int StackDepth => focusStack.Count;

        public KeyboardInterceptionManager GlobalKeyHandlers => focusStack.Peek().Interceptors;

        public ConsoleControl FocusedControl { get => Get<ConsoleControl>(); set => Set(value); }

        public FocusManager()
        {
            focusStack = new Stack<FocusContext>();
            focusStack.Push(new FocusContext());
        }

        public void Add(ConsoleControl c)
        {
            // This method used to always add the control to the top of the stack, but that was a bad idea
            // because it meant that controls that were added below dialogs would be treated as if they
            // were a part of the dialog, and would be able to get focus on tab.  What's worse was that when the dialog
            // was closed, the controls would never be able to be focused again because they were being
            // tracked by the dialog's focus context, which was now gone.  So now we add the control to the
            // focus context that is appropriate for its FocusStackDepth, which may be on or below the top.

            if (focusStack.SelectMany(s => s.Controls).Contains(c)) throw new InvalidOperationException("Item already being tracked");
            var effectiveDepth = Math.Max(c.FocusStackDepth, c.Anscestors.Select(a => a.FocusStackDepth).MaxOrDefault(1));
            if (effectiveDepth > focusStack.Count + 1) throw new NotSupportedException($"{nameof(c.FocusStackDepth)} can only exceed the current focus stack depth by 1");
            c.FocusStackDepthInternal = effectiveDepth;
            if (focusStack.Count < c.FocusStackDepth) Push(c);
            focusStack.Reverse().ToArray()[c.FocusStackDepth-1].Controls.Add(c);
        }

        public void Remove(ConsoleControl c)
        {
            foreach (var context in focusStack)
            {
                context.Controls.Remove(c);
            }
            if(focusStack.Peek().Controls.None() && focusStack.Count > 1) Pop();
        }

        private void Push(ConsoleControl cause)
        {
            var containerContains = cause is Container && (cause as Container).Descendents.Contains(FocusedControl);
            var shouldClear = FocusedControl != null && FocusedControl != cause && containerContains == false;
            if (shouldClear)
            {
                ClearFocus();
            }
            focusStack.Push(new FocusContext());
            FirePropertyChanged(nameof(StackDepth));
        }

        private void Pop()
        {
            if (focusStack.Count == 1)
            {
                throw new InvalidOperationException("Cannot pop the last item off the focus stack");
            }

            focusStack.Pop();
            RestoreFocus();
            FirePropertyChanged(nameof(StackDepth));
        }

        public void SetFocus(ConsoleControl newFocusControl)
        {
            var index = focusStack.Peek().Controls.IndexOf(newFocusControl);
            if (index < 0)
            {
                throw new InvalidOperationException("The given control is not in the focus stack. ");
            }

            if (newFocusControl.CanFocus == false)
            {
                if(newFocusControl is Container)
                {
                    var firstChild = (newFocusControl as Container).Descendents
                        .Where(c => c.CanFocus)
                        .FirstOrDefault();
                    if(firstChild != null)
                    {
                        SetFocus(firstChild);
                        return;
                    }
                }

                throw new InvalidOperationException("The given control cannot be focused");
            }
            else if (newFocusControl == FocusedControl)
            {
                // done
            }
            else
            {
                var oldFocusedControl = FocusedControl;
                if (oldFocusedControl != null)
                {
                    oldFocusedControl.HasFocus = false;
                }
                newFocusControl.HasFocus = true;
                FocusedControl = newFocusControl;

                focusStack.Peek().FocusIndex = index;

                if (oldFocusedControl != null)
                {
                    oldFocusedControl.FireFocused(false);
                }

                if (FocusedControl != null)
                {
                    FocusedControl.FireFocused(true);
                }
            }
        }

        public void MoveFocus(bool forward = true)
        {
            if (focusStack.Peek().Controls.Count == 0)
            {
                return;
            }

            int initialPosition = focusStack.Peek().FocusIndex;

            if(FocusedControl != null)
            {
                var focusedIndex = focusStack.Peek().Controls.IndexOf(FocusedControl);
                if(focusedIndex != initialPosition)
                {
                    focusStack.Peek().FocusIndex = focusedIndex;
                    initialPosition = focusedIndex;
                }
            }

            do
            {
                bool wrapped = CycleFocusIndex(forward);
                var nextControl = focusStack.Peek().Controls[focusStack.Peek().FocusIndex];
                if (nextControl.CanFocus && nextControl.TabSkip == false)
                {
                    SetFocus(nextControl);
                    return;
                }

                if (wrapped && initialPosition < 0) break;
            }
            while (focusStack.Peek().FocusIndex != initialPosition);
        }

        public void RestoreFocus()
        {
            if (focusStack.Peek().Controls.Where(c => c.CanFocus).Count() == 0)
            {
                return;
            }

            int initialPosition = focusStack.Peek().FocusIndex;

            bool skipOnce = true;
            do
            {
                bool wrapped = false;
                if (skipOnce)
                {
                    skipOnce = false;
                }
                else
                {
                    wrapped = CycleFocusIndex(true);
                }

                var newFocusIndex = Math.Max(0, Math.Min(focusStack.Peek().FocusIndex, focusStack.Peek().Controls.Count - 1));
                focusStack.Peek().FocusIndex = newFocusIndex;
                var nextControl = focusStack.Peek().Controls[focusStack.Peek().FocusIndex];
                if (nextControl.CanFocus)
                {
                    SetFocus(nextControl);
                    return;
                }

                if (wrapped && initialPosition < 0) break;
            }
            while (focusStack.Peek().FocusIndex != initialPosition);
        }

        public void ClearFocus()
        {
            if (Current?.ShouldContinue == false) return;
            if (FocusedControl != null)
            {
                FocusedControl.HasFocus = false;
                FocusedControl.FireFocused(false);
                FocusedControl = null;
            }
        }

        private bool CycleFocusIndex(bool forward)
        {
            if (forward)
            {
                focusStack.Peek().FocusIndex++;
            }
            else
            {
                focusStack.Peek().FocusIndex--;
            }

            if (focusStack.Peek().FocusIndex >= focusStack.Peek().Controls.Count)
            {
                focusStack.Peek().FocusIndex = 0;
                return true;
            }
            else if (focusStack.Peek().FocusIndex < 0)
            {
                focusStack.Peek().FocusIndex = focusStack.Peek().Controls.Count - 1;
                return true;
            }

            return false;
        }
    }
}