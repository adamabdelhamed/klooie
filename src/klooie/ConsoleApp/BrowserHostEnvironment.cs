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
}
