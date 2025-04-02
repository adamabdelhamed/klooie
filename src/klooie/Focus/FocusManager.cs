using System.Diagnostics;

namespace klooie;




public partial class FocusManager : Recyclable,  IObservableObject
{
    private static readonly long CycleThrottlerIntervalTicks = Stopwatch.Frequency / 1000 * 25; // 25ms in ticks
    private long lastCycleThrottlerCheck;
    private Queue<KeyRequest> sendKeys = new Queue<KeyRequest>();
    private DateTime lastKeyPressTime = DateTime.MinValue;
    private ConsoleKey lastKey;

    private Event<ConsoleKeyInfo> _globalKeyPressed;
    public Event<ConsoleKeyInfo> GlobalKeyPressed    { get => _globalKeyPressed ?? (_globalKeyPressed = EventPool<ConsoleKeyInfo>.Instance.Rent()); }

    protected override void OnReturn()
    {
        base.OnReturn();
        _globalKeyPressed?.Dispose();
        _globalKeyPressed = null;
        sendKeys.Clear();
        lastKeyPressTime = DateTime.MinValue;
        lastKey = default;
        FocusedControl = null;
    }

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

    public class KeyboardInterceptionManager
    {
        private class HandlerContext
        {
            internal Dictionary<ConsoleKey, List<KeyboardAction>> NakedHandlers { get; private set; } = new Dictionary<ConsoleKey, List<KeyboardAction>>();
            internal Dictionary<ConsoleKey, List<KeyboardAction>> AltHandlers { get; private set; } = new Dictionary<ConsoleKey, List<KeyboardAction>>();
            internal Dictionary<ConsoleKey, List<KeyboardAction>> ShiftHandlers { get; private set; } = new Dictionary<ConsoleKey, List<KeyboardAction>>();
            internal Dictionary<ConsoleKey, List<KeyboardAction>> ControlHandlers { get; private set; } = new Dictionary<ConsoleKey, List<KeyboardAction>>();
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
            bool noModifier = !alt && !shift && !control;

            int handlerCount = 0;
            try
            {
                var ctx = handlerStack.Peek();

                if (noModifier && ctx.NakedHandlers.ContainsKey(keyInfo.Key))
                {
                    ctx.NakedHandlers[keyInfo.Key][^1].Invoke(keyInfo);
                    handlerCount++;
                }

                if (alt && ctx.AltHandlers.ContainsKey(keyInfo.Key))
                {
                    ctx.AltHandlers[keyInfo.Key][^1].Invoke(keyInfo);
                    handlerCount++;
                }

                if (shift && ctx.ShiftHandlers.ContainsKey(keyInfo.Key))
                {
                    ctx.ShiftHandlers[keyInfo.Key][^1].Invoke(keyInfo);
                    handlerCount++;
                }

                if (control && ctx.ControlHandlers.ContainsKey(keyInfo.Key))
                {
                    ctx.ControlHandlers[keyInfo.Key][^1].Invoke(keyInfo);
                    handlerCount++;
                }

                return handlerCount > 0;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private Dictionary<ConsoleKey, List<KeyboardAction>> GetDictionaryForModifier(ConsoleModifiers? modifier)
        {
            var ctx = handlerStack.Peek();
            if (!modifier.HasValue || modifier == ConsoleModifiers.None) return ctx.NakedHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Alt)) return ctx.AltHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Shift)) return ctx.ShiftHandlers;
            else if (modifier.Value.HasFlag(ConsoleModifiers.Control)) return ctx.ControlHandlers;

            throw new ArgumentException($"Unsupported modifier: {modifier.Value}");
        }

        private Recyclable PushHandler(Dictionary<ConsoleKey, List<KeyboardAction>> dictionary, ConsoleKey key, KeyboardAction handlerAction)
        {
            if (!dictionary.TryGetValue(key, out var handlerList))
            {
                handlerList = new List<KeyboardAction>();
                dictionary.Add(key, handlerList);
            }

            handlerList.Add(handlerAction);
            int index = handlerList.Count - 1;

 
            handlerAction.OnDisposed(() =>
            {
                // Remove the handler from the list only if it's still there
                if (index >= 0 && index < handlerList.Count && handlerList[index] == handlerAction)
                {
                    handlerList.RemoveAt(index);
                }
                else
                {
                    handlerList.Remove(handlerAction); // fallback to remove by value
                }

                if (handlerList.Count == 0)
                {
                    dictionary.Remove(key);
                }
            });

            return handlerAction;
        }

        private Recyclable PushUnmanaged(ConsoleKey key, ConsoleModifiers? modifier, Action<ConsoleKeyInfo> handler)
        {
            var target = GetDictionaryForModifier(modifier);
            var handlerAction = KeyboardActionPool.Instance.Rent();
            handlerAction.Callback = handler;
            return PushHandler(target, key, handlerAction);
        }

        private Recyclable PushUnmanaged(ConsoleKey key, ConsoleModifiers? modifier, object scope, Action<object, ConsoleKeyInfo> handler)
        {
            var target = GetDictionaryForModifier(modifier);
            var handlerAction = KeyboardActionPool.Instance.Rent();
            handlerAction.Scope = scope;
            handlerAction.ScopedCallback = handler;

            return PushHandler(target, key, handlerAction);
        }

        public void PushForLifetime(ConsoleKey key, ConsoleModifiers? modifier, object scope, Action<object, ConsoleKeyInfo> handler, ILifetime manager)
        {
            manager.OnDisposed(PushUnmanaged(key, modifier, scope, handler));
        }

        /// <summary>
        /// Pushes this handler onto its appropriate handler stack for the given lifetime
        /// </summary>
        /// <param name="key">the key ti handle</param>
        /// <param name="modifier">the modifier, or null if you want to handle the unmodified keypress</param>
        /// <param name="handler">the code to run when the key input is intercepted</param>
        /// <param name="manager">the lifetime of the handlers registration</param>
        public void PushForLifetime(ConsoleKey key, ConsoleModifiers? modifier, Action<ConsoleKeyInfo> handler, ILifetime manager)
        {
            manager.OnDisposed(PushUnmanaged(key, modifier, handler));
        }

        /// <summary>
        /// Pushes this handler onto its appropriate handler stack for the given lifetime
        /// </summary>
        /// <param name="key">the key ti handle</param>
        /// <param name="modifier">the modifier, or null if you want to handle the unmodified keypress</param>
        /// <param name="handler">the code to run when the key input is intercepted</param>
        /// <param name="manager">the lifetime of the handlers registration</param>
        public void PushForLifetime(ConsoleKey key, ConsoleModifiers? modifier, Action handler, ILifetime manager)
        {
            PushForLifetime(key, modifier, (k) => { handler(); }, manager);
        }
    }



    private List<FocusContext> focusStack;

    public List<FocusContext> Stack => focusStack;

    public partial int StackDepth { get; set; }

    public KeyboardInterceptionManager GlobalKeyHandlers => focusStack[focusStack.Count - 1].Interceptors;

    public partial ConsoleControl FocusedControl { get; set; }

    /// <summary>
    /// When key throttling is enabled this lets you set the minimum time that must
    /// elapse before we forward a key press to the app, provided it is the same key
    /// that was most recently pressed.
    /// </summary>
    public TimeSpan MinTimeBetweenKeyPresses { get; set; } = TimeSpan.FromMilliseconds(35);
    /// <summary>
    /// True by default. When true, discards key presses that come in too fast
    /// likely because the user is holding the key down. You can set the
    /// MinTimeBetweenKeyPresses property to suit your needs.
    /// </summary>
    public bool KeyThrottlingEnabled { get; set; } = true;

    /// <summary>
    /// An event that fires when key input has been throttled. Only fired
    /// when KeyThrottlingEnabled is true.
    /// </summary>
    public Event OnKeyInputThrottled { get; private set; } = new Event();

    public FocusManager()
    {
        focusStack = new List<FocusContext>();
        focusStack.Add(new FocusContext());
        StackDepth = 1;
        ConsoleApp.Current.LayoutRoot.DescendentAdded.SubscribeWithPriority(Add, ConsoleApp.Current);
        ConsoleApp.Current.LayoutRoot.DescendentRemoved.SubscribeWithPriority(Remove, ConsoleApp.Current);
        ConsoleApp.Current.EndOfCycle.Subscribe(Cycle, ConsoleApp.Current);
    }

    private void Cycle()
    {

        // Check if enough time has passed since the last key check
        var delta = Stopwatch.GetTimestamp() - lastCycleThrottlerCheck;
        if (delta >= CycleThrottlerIntervalTicks)
        {
            lastCycleThrottlerCheck = Stopwatch.GetTimestamp();

            if (ConsoleProvider.Current.KeyAvailable)
            {
                var info = ConsoleProvider.Current.ReadKey(true);

                var effectiveMinTimeBetweenKeyPresses = MinTimeBetweenKeyPresses;
                if (KeyThrottlingEnabled && info.Key == lastKey && DateTime.UtcNow - lastKeyPressTime < effectiveMinTimeBetweenKeyPresses)
                {
                    // The user is holding the key down and throttling is enabled
                    OnKeyInputThrottled.Fire();
                }
                else
                {
                    lastKeyPressTime = DateTime.UtcNow;
                    lastKey = info.Key;
                    HandleKeyInput(info);
                }
            }
        }

        if (sendKeys.Count > 0)
        {
            var request = sendKeys.Dequeue();
            
            HandleKeyInput(request.Info);
            request.TaskSource.SetResult(true);
            
        }
    }

    private void HandleKeyInput(ConsoleKeyInfo info)
    {
        ConsoleApp.Current.GlobalKeyPressed.Fire(info);

        if (GlobalKeyHandlers.TryIntercept(info))
        {
            // great, it was handled
        }
        else if (info.Key == ConsoleKey.Tab)
        {
            MoveFocus(info.Modifiers.HasFlag(ConsoleModifiers.Shift) == false);
        }
        else if (info.Key == ConsoleKey.Escape)
        {
            ConsoleApp.Current.Stop();
            return;
        }
        else if (FocusedControl != null)
        {
            FocusedControl.HandleKeyInput(info);
        }
        else
        {
            // not handled
        }
        ConsoleApp.Current.RequestPaint();
    }


    /// <summary>
    /// Simulates a key press
    /// </summary>
    /// <param name="key">the key press info</param>
    public Task SendKey(ConsoleKeyInfo key)
    {
        var tcs = new TaskCompletionSource<bool>();
        ConsoleApp.Current.Invoke(() =>
        {
            sendKeys.Enqueue(new KeyRequest() { Info = key, TaskSource = tcs });
        });
        return tcs.Task;
    }


    private int FindEffectiveDepth(ConsoleControl c)
    {
        var ansector = c.Parent;
        var max = Math.Max(1, c.FocusStackDepth);
        while (ansector != null)
        {
            max = Math.Max(max, ansector.FocusStackDepth);
            ansector = ansector.Parent;
        }
        return max;
    }

    private void CheckNotAlreadyBeingTracked(ConsoleControl c)
    {
        for (var i = 0; i < focusStack.Count; i++)
        {
            for (var j = 0; j < focusStack[i].Controls.Count; j++)
            {
                if (focusStack[i].Controls[j] == c) throw new InvalidOperationException("Item already being tracked");
            }
        }
    }


    public void Add(ConsoleControl c)
    {
        // This method used to always add the control to the top of the stack, but that was a bad idea
        // because it meant that controls that were added below dialogs would be treated as if they
        // were a part of the dialog, and would be able to get focus on tab.  What's worse was that when the dialog
        // was closed, the controls would never be able to be focused again because they were being
        // tracked by the dialog's focus context, which was now gone.  So now we add the control to the
        // focus context that is appropriate for its FocusStackDepth, which may be on or below the top.


        CheckNotAlreadyBeingTracked(c);
        var effectiveDepth = FindEffectiveDepth(c);
        if (effectiveDepth > focusStack.Count + 1) throw new NotSupportedException($"{nameof(c.FocusStackDepth)} can only exceed the current focus stack depth by 1");
        c.FocusStackDepthInternal = effectiveDepth;
        if (focusStack.Count < c.FocusStackDepth) Push(c);
        focusStack[c.FocusStackDepth - 1].Controls.Add(c);
    }

    public void Remove(ConsoleControl c)
    {
        bool cleared = false;
        if (FocusedControl == c)
        {
            ClearFocus();
            cleared = true;
        }

        var removedCount = 0;
        foreach (var context in focusStack)
        {
            var removed = context.Controls.Remove(c);
            removedCount += removed ? 1 : 0;
        }
        if (focusStack.Last().Controls.None() && focusStack.Count > 1) Pop();

        if (cleared)
        {
            RestoreFocus();
        }
    }

    private void Push(ConsoleControl cause)
    {
        var containerContains = cause is Container && (cause as Container).Descendents.Contains(FocusedControl);
        var shouldClear = FocusedControl != null && FocusedControl != cause && containerContains == false;
        if (shouldClear)
        {
            ClearFocus();
        }
        focusStack.Add(new FocusContext());
        StackDepth++;
    }

    private void Pop()
    {
        if (focusStack.Count == 1)
        {
            throw new InvalidOperationException("Cannot pop the last item off the focus stack");
        }

        focusStack.RemoveAt(focusStack.Count - 1);
        RestoreFocus();
        StackDepth--;
    }

    public void SetFocus(ConsoleControl newFocusControl)
    {
        var index = focusStack[focusStack.Count - 1].Controls.IndexOf(newFocusControl);
        if (index < 0)
        {
            throw new InvalidOperationException("The given control is not in the focus stack. ");
        }

        if (CanReceiveFocusNow(newFocusControl) == false)
        {
            if (newFocusControl is Container)
            {
                var firstChild = (newFocusControl as Container).Descendents
                    .Where(c => CanReceiveFocusNow(c))
                    .FirstOrDefault();
                if (firstChild != null)
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

            focusStack[focusStack.Count - 1].FocusIndex = index;

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
        if (focusStack[focusStack.Count - 1].Controls.Count == 0)
        {
            return;
        }

        int initialPosition = focusStack[focusStack.Count - 1].FocusIndex;

        if (FocusedControl != null)
        {
            var focusedIndex = focusStack[focusStack.Count - 1].Controls.IndexOf(FocusedControl);
            if (focusedIndex != initialPosition)
            {
                focusStack[focusStack.Count - 1].FocusIndex = focusedIndex;
                initialPosition = focusedIndex;
            }
        }

        do
        {
            bool wrapped = CycleFocusIndex(forward);
            var nextControl = focusStack[focusStack.Count - 1].Controls[focusStack[focusStack.Count - 1].FocusIndex];
            if (CanReceiveFocusNow(nextControl) && nextControl.TabSkip == false)
            {
                SetFocus(nextControl);
                return;
            }

            if (wrapped && initialPosition < 0) break;
        }
        while (focusStack[focusStack.Count - 1].FocusIndex != initialPosition);
    }

    public void RestoreFocus()
    {
        if (focusStack[focusStack.Count - 1].Controls.Where(c => CanReceiveFocusNow(c)).Count() == 0)
        {
            return;
        }

        int initialPosition = focusStack[focusStack.Count - 1].FocusIndex;

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

            var newFocusIndex = Math.Max(0, Math.Min(focusStack[focusStack.Count - 1].FocusIndex, focusStack[focusStack.Count - 1].Controls.Count - 1));
            focusStack[focusStack.Count - 1].FocusIndex = newFocusIndex;
            var nextControl = focusStack[focusStack.Count - 1].Controls[focusStack[focusStack.Count - 1].FocusIndex];
            if (CanReceiveFocusNow(nextControl))
            {
                SetFocus(nextControl);
                return;
            }

            if (wrapped && initialPosition < 0) break;
        }
        while (focusStack[focusStack.Count - 1].FocusIndex != initialPosition);
    }

    public static bool CanReceiveFocusNow(ConsoleControl c) => c.CanFocus && c.IsVisibleAndAllParentsVisible;

    public void ClearFocus()
    {
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
            focusStack[focusStack.Count - 1].FocusIndex++;
        }
        else
        {
            focusStack[focusStack.Count - 1].FocusIndex--;
        }

        if (focusStack[focusStack.Count - 1].FocusIndex >= focusStack[focusStack.Count - 1].Controls.Count)
        {
            focusStack[focusStack.Count - 1].FocusIndex = 0;
            return true;
        }
        else if (focusStack[focusStack.Count - 1].FocusIndex < 0)
        {
            focusStack[focusStack.Count - 1].FocusIndex = focusStack[focusStack.Count - 1].Controls.Count - 1;
            return true;
        }

        return false;
    }

    private class KeyRequest
    {
        public ConsoleKeyInfo Info { get; set; }
        public TaskCompletionSource<bool> TaskSource { get; set; }
    }
}

public class KeyboardAction : Recyclable
{
    public Action<ConsoleKeyInfo>? Callback { get; set; }
    public object? Scope { get; set; }
    public Action<object, ConsoleKeyInfo>? ScopedCallback { get; set; }

    protected override void OnInit()
    {
        Callback = null;
        ScopedCallback = null;
        Scope = null;
    }

    public void Invoke(ConsoleKeyInfo keyInfo)
    {
        if (ScopedCallback != null)
        {
            // We have a scope-based callback
            ScopedCallback(Scope, keyInfo);
        }
        else
        {
            // Fallback to the original approach
            Callback?.Invoke(keyInfo);
        }
    }
}