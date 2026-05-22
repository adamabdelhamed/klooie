namespace klooie.blazor.Hosting;

public sealed record KlooieBlazorAppRegistration(
    string Route,
    string DisplayName,
    string Description,
    Func<Task> RunAsync);
