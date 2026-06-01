namespace klooie.blazor.Hosting;

public sealed record KlooieBlazorAppRegistration(
    string Route,
    string DisplayName,
    string Description,
    Func<Task> RunAsync,
    KlooieBlazorMobileOptions MobileOptions);

public sealed record KlooieBlazorMobileOptions(bool RequireHorizontal = false, bool TouchTriggerToggle = false);
