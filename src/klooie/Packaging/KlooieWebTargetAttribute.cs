namespace klooie;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class KlooieWebTargetAttribute : Attribute
{
    public string? Route { get; set; }

    public string? DisplayName { get; set; }

    public string? BrowserTitle { get; set; }

    public string? PwaName { get; set; }

    public string? PwaShortName { get; set; }

    public string? Description { get; set; }

    public string? IconPath { get; set; }

    public string? ThemeColor { get; set; }

    public string? BackgroundColor { get; set; }

    public bool RequireHorizontal { get; set; }

    public bool TouchTriggerToggle { get; set; }
}
