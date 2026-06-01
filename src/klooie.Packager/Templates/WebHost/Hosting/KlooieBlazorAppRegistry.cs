namespace klooie.blazor.Hosting;

public sealed class KlooieBlazorAppRegistry
{
    private readonly List<KlooieBlazorAppRegistration> apps = new();

    public IReadOnlyList<KlooieBlazorAppRegistration> Apps => apps;

    public void Register(string route, string displayName, string description, Func<Task> runAsync, KlooieBlazorMobileOptions? mobileOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(runAsync);

        route = route.Trim('/');
        if (apps.Any(app => string.Equals(app.Route, route, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A klooie app is already registered for route '{route}'.");
        }

        apps.Add(new KlooieBlazorAppRegistration(route, displayName, description, runAsync, mobileOptions ?? new KlooieBlazorMobileOptions()));
    }

    public KlooieBlazorAppRegistration? Find(string route)
    {
        route = route.Trim('/');
        return apps.FirstOrDefault(app => string.Equals(app.Route, route, StringComparison.OrdinalIgnoreCase));
    }
}
