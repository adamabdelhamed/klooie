namespace klooie.blazor.Hosting;

public sealed record KlooieBlazorAppRegistration(
    string Route,
    string DisplayName,
    string Description,
    Func<Task> RunAsync,
    KlooieBlazorMobileOptions MobileOptions,
    KlooieBlazorBrowserMetadata BrowserMetadata);

public sealed record KlooieBlazorMobileOptions(bool RequireHorizontal = false, bool TouchTriggerToggle = false, bool EnableZoom = true);

public sealed record KlooieBlazorBrowserMetadata(
    string BrowserTitle,
    string PwaName,
    string PwaShortName,
    string Description,
    string ThemeColor = "#000000",
    string BackgroundColor = "#000000",
    string? FaviconPath = null,
    string? AppIconPath = null)
{
    public static KlooieBlazorBrowserMetadata FromDisplayName(string displayName, string description)
    {
        return new KlooieBlazorBrowserMetadata(displayName, displayName, displayName, description);
    }
}
