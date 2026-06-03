namespace klooie;

public sealed partial class Controller
{
    private sealed class ButtonBinding
    {
        public required Action Action { get; init; }
    }

    private sealed class FocusedButtonBinding
    {
        public required ConsoleControl Control { get; init; }
        public required Action Action { get; init; }
    }

    private sealed class TriggerBinding
    {
        public required Action? LeftTrigger { get; init; }
        public required Action? RightTrigger { get; init; }
    }

    private sealed class TriggerHoldBinding
    {
        public required Action? LeftStart { get; init; }
        public required Action? LeftEnd { get; init; }
        public required Action? RightStart { get; init; }
        public required Action? RightEnd { get; init; }
    }

    private sealed class InputContext
    {
        public Dictionary<GamePadButton, List<ButtonBinding>> ButtonBindings { get; } = new();
        public Dictionary<GamePadButton, List<FocusedButtonBinding>> FocusedButtonBindings { get; } = new();
    }

    private readonly List<InputContext> contextStack = new() { new InputContext() };
    private readonly InputContext globalContext = new();
    private readonly List<TriggerBinding> triggerBindings = new();
    private readonly List<TriggerHoldBinding> triggerHoldBindings = new();
    private int leftTriggerDrivenRefCount;
    private int rightTriggerDrivenRefCount;
    private bool leftTriggerSuppressedUntilRelease;
    private bool rightTriggerSuppressedUntilRelease;
    private Action? leftTriggerHandler;
    private Action? rightTriggerHandler;
    private Action? leftTriggerHoldStarted;
    private Action? leftTriggerHoldEnded;
    private Action? rightTriggerHoldStarted;
    private Action? rightTriggerHoldEnded;
 

    public void InitializeFocusStackBindings()
    {
        var app = ConsoleApp.Current;
        if (app == null) return;

        app.FocusStackDepthChanged.Subscribe(() =>
        {
            PruneContexts(app.FocusStackDepth);
            RefreshTriggerHandlers(resetTriggerState: true);
            RefreshTriggerHoldHandlers(resetTriggerState: true);
            NotifyBindingStateChanged();
        }, this);
    }

    public void BindButton(GamePadButton button, Action handler, ILifetime lt)
    {
        if (lt is ConsoleControl control && control.HasBeenAddedToVisualTree == false)
        {
            control.Ready.SubscribeOnce(() => BindButton(button, handler, control));
            return;
        }

        var context = GetOrCreateContextForLifetime(lt);
        BindButton(context, button, handler, lt);
    }

    public void BindButton(ControllerButtonId button, Action handler, ILifetime lt) => BindButton(GetButton(button), handler, lt);
    public void BindFocusedButton(GamePadButton button, Action handler, ConsoleControl control)
    {
        if (control.HasBeenAddedToVisualTree == false)
        {
            control.Ready.SubscribeOnce(() => BindFocusedButton(button, handler, control));
            return;
        }

        var context = GetOrCreateContextForLifetime(control);
        if (context.FocusedButtonBindings.TryGetValue(button, out var bindings) == false)
        {
            bindings = new List<FocusedButtonBinding>();
            context.FocusedButtonBindings.Add(button, bindings);
        }

        for (var i = 0; i < bindings.Count; i++)
        {
            if (ReferenceEquals(bindings[i].Control, control))
            {
                throw new InvalidOperationException($"Focused handler for {button.Name} already exists for this control");
            }
        }

        var binding = new FocusedButtonBinding { Control = control, Action = handler };
        bindings.Add(binding);
        NotifyBindingStateChanged();

        control.OnDisposed(() =>
        {
            if (RemoveFocusedBinding(context, button, binding)) NotifyBindingStateChanged();
        });
    }

    public void BindFocusedButton(ControllerButtonId button, Action handler, ConsoleControl control) => BindFocusedButton(GetButton(button), handler, control);

    public void BindGlobalButton(GamePadButton button, Action handler, ILifetime lt) => BindButton(globalContext, button, handler, lt);
    public void BindGlobalButton(ControllerButtonId button, Action handler, ILifetime lt) => BindGlobalButton(GetButton(button), handler, lt);
    public bool TryInvokeBoundButtonOnly(ControllerButtonId button) => TryInvokeBoundButtonOnly(GetButton(button));

    public void SetTriggerHandlers(Action? leftTrigger, Action? rightTrigger, ILifetime lt)
    {
        var binding = new TriggerBinding
        {
            LeftTrigger = leftTrigger,
            RightTrigger = rightTrigger,
        };
        triggerBindings.Add(binding);
        RefreshTriggerHandlers(resetTriggerState: true);
        NotifyBindingStateChanged();

        lt.OnDisposed(() =>
        {
            if (triggerBindings.Remove(binding))
            {
                RefreshTriggerHandlers(resetTriggerState: true);
                NotifyBindingStateChanged();
            }
        });
    }

    public void SetTriggerHoldHandlers(Action? leftStart, Action? leftEnd, Action? rightStart, Action? rightEnd, ILifetime lt)
    {
        var binding = new TriggerHoldBinding
        {
            LeftStart = leftStart,
            LeftEnd = leftEnd,
            RightStart = rightStart,
            RightEnd = rightEnd,
        };
        triggerHoldBindings.Add(binding);
        RefreshTriggerHoldHandlers(resetTriggerState: true);
        NotifyBindingStateChanged();

        lt.OnDisposed(() =>
        {
            if (triggerHoldBindings.Remove(binding))
            {
                RefreshTriggerHoldHandlers(resetTriggerState: true);
                NotifyBindingStateChanged();
            }
        });
    }

    public void DriveTriggerForLifetime(ControllerButtonId trigger, ILifetime lt)
    {
        switch (trigger)
        {
            case ControllerButtonId.LeftTrigger:
                leftTriggerDrivenRefCount++;
                lt.OnDisposed(() => leftTriggerDrivenRefCount = Math.Max(0, leftTriggerDrivenRefCount - 1));
                break;
            case ControllerButtonId.RightTrigger:
                rightTriggerDrivenRefCount++;
                lt.OnDisposed(() => rightTriggerDrivenRefCount = Math.Max(0, rightTriggerDrivenRefCount - 1));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Only triggers can be driven for a lifetime.");
        }
    }

    public bool IsTriggerDriven(ControllerButtonId trigger) => trigger switch
    {
        ControllerButtonId.LeftTrigger => leftTriggerDrivenRefCount > 0,
        ControllerButtonId.RightTrigger => rightTriggerDrivenRefCount > 0,
        _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Only triggers support driven state."),
    };

    public bool AreTriggerHandlersActive(Action? leftTrigger, Action? rightTrigger)
        => ReferenceEquals(leftTriggerHandler, leftTrigger) && ReferenceEquals(rightTriggerHandler, rightTrigger);

    public void FlushHeldTriggersUntilRelease()
    {
        SuppressHeldTriggersUntilRelease();
        ResetTriggerRuntimeState();
    }

    public void FlushActiveInputForFocusLoss()
    {
        FlushButtonForFocusLoss(A);
        FlushButtonForFocusLoss(B);
        FlushButtonForFocusLoss(X);
        FlushButtonForFocusLoss(Y);
        FlushButtonForFocusLoss(Start);
        FlushButtonForFocusLoss(View);
        FlushButtonForFocusLoss(Home);
        FlushButtonForFocusLoss(DPadUp);
        FlushButtonForFocusLoss(DPadDown);
        FlushButtonForFocusLoss(DPadLeft);
        FlushButtonForFocusLoss(DPadRight);
        FlushButtonForFocusLoss(LeftBumper);
        FlushButtonForFocusLoss(RightBumper);
        FlushTriggerForFocusLoss(LeftTrigger, ControllerButtonId.LeftTrigger, leftTriggerHoldEnded);
        FlushTriggerForFocusLoss(RightTrigger, ControllerButtonId.RightTrigger, rightTriggerHoldEnded);
        LeftStick.HandleInput(default, false, suppressMoveEvent: false);
        RightStick.HandleInput(default, false, suppressMoveEvent: false);
        leftTriggerSuppressedUntilRelease = false;
        rightTriggerSuppressedUntilRelease = false;
    }

    public bool PollTrigger(GamePadButton trigger, bool isDown, long nowTicks, TimeSpan repeatInterval)
    {
        if (ReferenceEquals(trigger, LeftTrigger) && leftTriggerSuppressedUntilRelease)
        {
            if (isDown == false)
            {
                leftTriggerSuppressedUntilRelease = false;
            }

            trigger.SetDownState(false);
            trigger.LastRepeatTicks = 0;
            return false;
        }

        if (ReferenceEquals(trigger, RightTrigger) && rightTriggerSuppressedUntilRelease)
        {
            if (isDown == false)
            {
                rightTriggerSuppressedUntilRelease = false;
            }

            trigger.SetDownState(false);
            trigger.LastRepeatTicks = 0;
            return false;
        }

        var inputDetected = false;

        if (ReferenceEquals(trigger, LeftTrigger))
        {
            if (isDown && trigger.IsDown == false)
            {
                leftTriggerHoldStarted?.Invoke();
                inputDetected = true;
            }
            else if (isDown == false && trigger.IsDown)
            {
                leftTriggerHoldEnded?.Invoke();
                inputDetected = true;
            }

            if (isDown && (trigger.IsDown == false || nowTicks - trigger.LastRepeatTicks >= repeatInterval.Ticks))
            {
                if (leftTriggerHandler != null)
                {
                    leftTriggerHandler();
                    trigger.LastRepeatTicks = nowTicks;
                    inputDetected = true;
                }
            }
        }
        else if (ReferenceEquals(trigger, RightTrigger))
        {
            if (isDown && trigger.IsDown == false)
            {
                rightTriggerHoldStarted?.Invoke();
                inputDetected = true;
            }
            else if (isDown == false && trigger.IsDown)
            {
                rightTriggerHoldEnded?.Invoke();
                inputDetected = true;
            }

            if (isDown && (trigger.IsDown == false || nowTicks - trigger.LastRepeatTicks >= repeatInterval.Ticks))
            {
                if (rightTriggerHandler != null)
                {
                    rightTriggerHandler();
                    trigger.LastRepeatTicks = nowTicks;
                    inputDetected = true;
                }
            }
        }

        trigger.SetDownState(isDown);

        if (inputDetected) AnyInput.Fire();
        return inputDetected;
    }

    public bool PollTrigger(ControllerButtonId trigger, bool isDown, long nowTicks, TimeSpan repeatInterval) => PollTrigger(GetButton(trigger), isDown, nowTicks, repeatInterval);
 
    private InputContext GetOrCreateContextForLifetime(ILifetime lifetime)
    {
        var desiredDepth = lifetime is ConsoleControl control
            ? Math.Max(1, control.FocusStackDepth)
            : Math.Max(1, (ConsoleApp.Current ?? throw new InvalidOperationException("ConsoleApp.Current is null")).FocusStackDepth);

        while (contextStack.Count < desiredDepth)
        {
            contextStack.Add(new InputContext());
        }

        return contextStack[desiredDepth - 1];
    }

    private void PruneContexts(int currentDepth)
    {
        var desiredDepth = Math.Max(1, currentDepth);
        while (contextStack.Count > desiredDepth)
        {
            contextStack.RemoveAt(contextStack.Count - 1);
        }
    }

    private void BindButton(InputContext context, GamePadButton button, Action handler, ILifetime lt)
    {
        var binding = new ButtonBinding { Action = handler };
        AddBinding(context, button, binding);
        NotifyBindingStateChanged();

        lt.OnDisposed(() =>
        {
            if (RemoveBinding(context, button, binding)) NotifyBindingStateChanged();
        });
    }

    private static void AddBinding(InputContext context, GamePadButton button, ButtonBinding binding)
    {
        if (context.ButtonBindings.TryGetValue(button, out var bindings) == false)
        {
            bindings = new List<ButtonBinding>();
            context.ButtonBindings.Add(button, bindings);
        }

        bindings.Add(binding);
    }

    private static bool RemoveBinding(InputContext context, GamePadButton button, ButtonBinding binding)
    {
        if (context.ButtonBindings.TryGetValue(button, out var list) == false) return false;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i], binding) == false) continue;

            list.RemoveAt(i);
            if (list.Count == 0) context.ButtonBindings.Remove(button);
            return true;
        }

        return false;
    }

    private static bool RemoveFocusedBinding(InputContext context, GamePadButton button, FocusedButtonBinding binding)
    {
        if (context.FocusedButtonBindings.TryGetValue(button, out var list) == false) return false;

        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i], binding) == false) continue;

            list.RemoveAt(i);
            if (list.Count == 0) context.FocusedButtonBindings.Remove(button);
            return true;
        }

        return false;
    }
 

    private bool TryInvokeBoundButtonOnly(GamePadButton button)
    {
        if (TryResolveBoundAction(button, out var action))
        {
            action();
            return true;
        }

        return false;
    }
     

    private bool TryResolveBoundAction(GamePadButton button, out Action action)
    {
        if (TryResolveBoundAction(globalContext, button, out action))
        {
            return true;
        }

        var app = ConsoleApp.Current;
        var activeDepth = Math.Max(1, app?.FocusStackDepth ?? 1);
        if (activeDepth > contextStack.Count)
        {
            action = null!;
            return false;
        }

        var activeContext = contextStack[activeDepth - 1];
        if (TryResolveBoundAction(activeContext, button, out action))
        {
            return true;
        }

        action = null!;
        return false;
    }

    private static bool TryResolveBoundAction(InputContext context, GamePadButton button, out Action action)
    {
        action = null!;

        if (TryResolveFocusedBoundAction(context, button, out action))
        {
            return true;
        }

        if (context.ButtonBindings.TryGetValue(button, out var bindings) == false || bindings.Count == 0)
        {
            return false;
        }

        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            action = bindings[i].Action;
            return true;
        }

        return false;
    }

    private static bool TryResolveFocusedBoundAction(InputContext context, GamePadButton button, out Action action)
    {
        action = null!;

        var focused = ConsoleApp.Current?.FocusedControl;
        if (focused == null) return false;
        if (context.FocusedButtonBindings.TryGetValue(button, out var bindings) == false || bindings.Count == 0) return false;

        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(bindings[i].Control, focused) == false) continue;

            action = bindings[i].Action;
            return true;
        }

        return false;
    }

    private void RefreshTriggerHandlers(bool resetTriggerState)
    {
        var previousLeft = leftTriggerHandler;
        var previousRight = rightTriggerHandler;

        leftTriggerHandler = null;
        rightTriggerHandler = null;

        if (triggerBindings.Count > 0)
        {
            var active = triggerBindings[^1];
            leftTriggerHandler = active.LeftTrigger;
            rightTriggerHandler = active.RightTrigger;
        }

        if (resetTriggerState &&
            (ReferenceEquals(previousLeft, leftTriggerHandler) == false ||
             ReferenceEquals(previousRight, rightTriggerHandler) == false))
        {
            FlushHeldTriggersUntilRelease();
        }
    }

    private void RefreshTriggerHoldHandlers(bool resetTriggerState)
    {
        var previousLeftStart = leftTriggerHoldStarted;
        var previousLeftEnd = leftTriggerHoldEnded;
        var previousRightStart = rightTriggerHoldStarted;
        var previousRightEnd = rightTriggerHoldEnded;

        leftTriggerHoldStarted = null;
        leftTriggerHoldEnded = null;
        rightTriggerHoldStarted = null;
        rightTriggerHoldEnded = null;

        if (triggerHoldBindings.Count > 0)
        {
            var active = triggerHoldBindings[^1];
            leftTriggerHoldStarted = active.LeftStart;
            leftTriggerHoldEnded = active.LeftEnd;
            rightTriggerHoldStarted = active.RightStart;
            rightTriggerHoldEnded = active.RightEnd;
        }

        if (resetTriggerState &&
            (ReferenceEquals(previousLeftStart, leftTriggerHoldStarted) == false ||
             ReferenceEquals(previousLeftEnd, leftTriggerHoldEnded) == false ||
             ReferenceEquals(previousRightStart, rightTriggerHoldStarted) == false ||
             ReferenceEquals(previousRightEnd, rightTriggerHoldEnded) == false))
        {
            FlushHeldTriggersUntilRelease();
        }
    }

    private void SuppressHeldTriggersUntilRelease()
    {
        if (LeftTrigger.IsDown) leftTriggerSuppressedUntilRelease = true;
        if (RightTrigger.IsDown) rightTriggerSuppressedUntilRelease = true;
    }

    private static void FlushButtonForFocusLoss(GamePadButton button)
    {
        if (button.IsDown == false) return;
        button.HandleState(false);
    }

    private static void FlushTriggerForFocusLoss(GamePadButton trigger, ControllerButtonId triggerId, Action? holdEnded)
    {
        if (trigger.IsDown == false)
        {
            trigger.LastRepeatTicks = 0;
            return;
        }

        holdEnded?.Invoke();
        trigger.HandleState(false);
        trigger.LastRepeatTicks = 0;
    }

    private void ResetTriggerRuntimeState()
    {
        var leftWasDown = LeftTrigger.IsDown;
        var rightWasDown = RightTrigger.IsDown;
        LeftTrigger.ResetState();
        RightTrigger.ResetState();
        if (leftWasDown) programmaticButtonReleased?.Fire(ControllerButtonId.LeftTrigger);
        if (rightWasDown) programmaticButtonReleased?.Fire(ControllerButtonId.RightTrigger);
    }

    public bool IsButtonEffectivelyBound(ControllerButtonId button) => TryResolveBoundAction(GetButton(button), out _);

    public bool IsTriggerEffectivelyBound(ControllerButtonId trigger)
    {
        if (trigger == ControllerButtonId.LeftTrigger) return leftTriggerHandler != null || leftTriggerHoldStarted != null || leftTriggerHoldEnded != null;
        if (trigger == ControllerButtonId.RightTrigger) return rightTriggerHandler != null || rightTriggerHoldStarted != null || rightTriggerHoldEnded != null;
        throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Only triggers can be queried for trigger bindings.");
    }
}
