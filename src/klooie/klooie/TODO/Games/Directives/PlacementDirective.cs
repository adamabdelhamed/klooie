using klooie.Gaming.Code;

namespace klooie.Gaming;
public abstract class PlacementDirective : EventDrivenDirective
{
    [ArgRequired]
    [ArgIgnoreSerialize(-1)]
    public virtual DynamicArg Left { get; set; } = new DynamicArg() { Argument = "-1" };

    [ArgRequired]
    [ArgIgnoreSerialize(-1)]
    public virtual DynamicArg Top { get; set; } = new DynamicArg() { Argument = "-1" };


    [ArgDefaultValue(-1)]
    [ArgShortcut(ArgShortcutPolicy.NoShortcut)]
    public DynamicArg ZIndex { get; set; } = new DynamicArg() { Argument = "-1" };

    public bool RealLineMode { get; set; }

    public List<string> Tags { get; set; } = new List<string>();

    public bool? NudgeAfterPlacement { get; set; }

    [ArgIgnore]
    public ConsoleControl Placed { get; private set; }

    [ArgIgnore]
    public Event<ConsoleControl> OnPlaced { get; private set; } = new Event<ConsoleControl>();

    public void Place(ConsoleControl element)
    {
        this.Placed = element;
        element.AddTags(Tags ?? Enumerable.Empty<string>());

        int? z = ZIndex.IntValue == -1 ? null : new int?(ZIndex.IntValue);
        var placement = GetPlacement();
        element.MoveTo(placement.Left, placement.Top, z);
        
        var explicitNudge = NudgeAfterPlacement.HasValue && NudgeAfterPlacement.Value;
        var implicitNudge = NudgeAfterPlacement.HasValue == false && element.ZIndex == 0;
        var nudge = implicitNudge || explicitNudge;
        if (element is GameCollider && nudge)
        {
            ((GameCollider)element).NudgeFree();
        }
        OnPlaced.Fire(element);
    }

    private LocF GetPlacement() => new LocF(Left.FloatValue, Top.FloatValue);
}
