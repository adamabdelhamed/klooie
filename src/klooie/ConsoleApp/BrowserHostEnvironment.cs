namespace klooie;

public static class BrowserHostEnvironment
{
    private static bool isMobileExperience;

    public static bool IsMobileExperience
    {
        get => isMobileExperience;
        set
        {
            if (isMobileExperience == value) return;
            isMobileExperience = value;
            MobileExperienceChanged.Fire(value);
        }
    }

    public static Event<bool> MobileExperienceChanged { get; } = Event<bool>.Create();

    public static Event<BrowserOverlayRequest> OverlayRequested { get; } = Event<BrowserOverlayRequest>.Create();

    public static void ShowOverlay(string id, string? title = null, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        OverlayRequested.Fire(new BrowserOverlayRequest(id, title, message));
    }
}

public sealed record BrowserOverlayRequest(string Id, string? Title = null, string? Message = null);
