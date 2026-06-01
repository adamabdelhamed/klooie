namespace klooie;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class KlooieWebTargetAttribute : Attribute
{
    public string? Route { get; set; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public bool RequireHorizontal { get; set; }

    public bool TouchTriggerToggle { get; set; }
}
