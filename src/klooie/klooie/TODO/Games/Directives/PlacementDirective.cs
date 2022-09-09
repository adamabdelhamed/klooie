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



    [ArgIgnore]
    public LocF? ManualPlacementLocation { get; set; }

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
        if (Tags != null && Tags.Count > 0)
        {
            element.AddTags(Tags);
        }

        int? z = ZIndex.IntValue == -1 ? null : new int?(ZIndex.IntValue);
        element.MoveTo(element.Left, element.Top, z);
       
        var placement = GetPlacement();
        element.MoveTo(placement.Left, placement.Top, z);
        

        var explicitNudge = NudgeAfterPlacement.HasValue && NudgeAfterPlacement.Value;
        var implicitNudge = NudgeAfterPlacement.HasValue == false && element.ZIndex == 0;

        if (element is GameCollider && (implicitNudge || explicitNudge))
        {
            ((GameCollider)element).NudgeFree();
        }
        OnPlaced.Fire(element);
    }

    public LocF GetPlacement()
    {
        if (ManualPlacementLocation.HasValue) return ManualPlacementLocation.Value;

        return new LocF(Left.FloatValue, Top.FloatValue);
    }
}
