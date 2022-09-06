namespace klooie.Gaming.Code;
public class WatchDirective : EventDrivenDirective
{
    [ArgRequired]
    [ArgPosition(0)]
    public string VariableName { get; set; }

    public ConsoleString DisplayName { get; set; }

    public string OffEvent { get; set; }

    [ArgCantBeCombinedWith(nameof(ValueForeground))]
    [ArgCantBeCombinedWith(nameof(ValueBackground))]
    public bool LargerIsWorse { get; set; }

    [ArgCantBeCombinedWith(nameof(ValueForeground))]
    [ArgCantBeCombinedWith(nameof(ValueBackground))]
    public float? YellowThreshold { get; set; }

    [ArgCantBeCombinedWith(nameof(ValueForeground))]
    [ArgCantBeCombinedWith(nameof(ValueBackground))]
    public float? RedThreshold { get; set; }

    public bool LocalOnly { get; set; }

    [ArgDefaultValue("Thread")]
    public ConsoleString ThreadDisplay { get; set; }

    public ConsoleColor? ValueForeground { get; set; }
    public ConsoleColor? ValueBackground { get; set; }

    [ArgDefaultValue("Watches")]
    public string Category { get; set; } = "Watches";

    [ArgIgnore]
    public Func<float> MaxValueFunc { get; set; }
    public float? MaxValue { get; set; }

    private ConsoleString Label => DisplayName ?? VariableName.ToConsoleString();

    private Game game => Game.Current;

    public override Task OnEventFired(object args)
    {
        if (Heap.Current.TryGetValue("ActiveThreads", out List<TimeThread> activeThreads) == false)
        {
            activeThreads = new List<TimeThread>();
            Heap.Current.Set(activeThreads, "ActiveThreads");
            TimeThread.ThreadStarted.Subscribe((thread) =>
            {
                activeThreads.Add(thread);
                thread.OnDisposed(() => activeThreads.Remove(thread));
            }, Game.Current);
        }

        var lifetime = Game.Current.CreateChildLifetime();

        if (LocalOnly == false)
        {
            Heap.Current.Sync(VariableName, () =>
            {
                var rawValue = Heap.Current.Get<object>(VariableName);
                UpdateValue(null, rawValue);
            }, lifetime);
        }

        Game.Current.Invoke(async () =>
        {
            while (lifetime.IsExpired == false)
            {
                foreach (var thread in activeThreads)
                {
                    if (thread.TryResolve(VariableName, out object val, true))
                    {
                        UpdateValue(thread, val);
                    }
                }
                await Game.Current.Delay(333);
            }
        });

        if (OffEvent != null)
        {
            Game.Current.Subscribe(OffEvent, (ev) => lifetime.Dispose(), lifetime);
        }

        return Task.CompletedTask;
    }

    private void UpdateValue(TimeThread thread, object rawValue)
    {
        ConsoleString displayValue;
        if (ValueForeground.HasValue || ValueBackground.HasValue)
        {
            displayValue = ("" + rawValue).ToConsoleString(ValueForeground, ValueBackground);
        }
        else if (RedThreshold.HasValue || YellowThreshold.HasValue)
        {
            var floatValue = rawValue == null ? 0 : float.Parse("" + rawValue);
            displayValue = ("" + rawValue).ToConsoleString(ThresholdColor(floatValue));
        }
        else
        {
            displayValue = ("" + rawValue).ToConsoleString();
        }

        var label = Label;
        var threadId = thread == null ? ConsoleString.Empty : thread.Id.ToString().ToConsoleString();
        if (threadId.Length > 0)
        {
            label = $"{ThreadDisplay} {threadId}:".ToConsoleString() + Label;
        }

        if ("watches".Equals(Category, StringComparison.OrdinalIgnoreCase))
        {
            // no more HUD
            //PlayerDirective.Current.CreateOrUpdateProperty("WatchGap".ToBlack(), ConsoleString.Empty, thread, category: Category);
            //PlayerDirective.Current.CreateOrUpdateProperty(Category.ToYellow(), ConsoleString.Empty, thread, category: Category);
        }

        float? fullness = null;

        MaxValueFunc = MaxValueFunc ?? (MaxValue.HasValue ? () => MaxValue.Value : MaxValueFunc);
        if (MaxValueFunc != null)
        {
            fullness = (float)rawValue / MaxValueFunc();
        }

        // no more HUD
        //PlayerDirective.Current.CreateOrUpdateProperty(label, displayValue, thread, category: Category, fullness: fullness);
    }

    private ConsoleColor ThresholdColor(float value)
    {
        if (LargerIsWorse)
        {
            if (RedThreshold.HasValue && value >= RedThreshold)
            {
                return ConsoleColor.Red;
            }
            else if (YellowThreshold.HasValue && value >= YellowThreshold)
            {
                return ConsoleColor.Yellow;
            }
            else
            {
                return ConsoleColor.Green;
            }
        }
        else
        {
            if (RedThreshold.HasValue && value <= RedThreshold)
            {
                return ConsoleColor.Red;
            }
            else if (YellowThreshold.HasValue && value <= YellowThreshold)
            {
                return ConsoleColor.Yellow;
            }
            else
            {
                return ConsoleColor.Green;
            }
        }
    }
}