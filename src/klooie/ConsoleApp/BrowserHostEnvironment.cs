namespace klooie;

public static class BrowserHostEnvironment
{
    private const int MaxUtmValueLength = 120;
    private static bool isMobileExperience;
    private static string queryString = "";
    private static readonly IReadOnlyDictionary<string, string> UtmParameterNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["utm_source"] = nameof(UtmSource),
        ["utm_medium"] = nameof(UtmMedium),
        ["utm_campaign"] = nameof(UtmCampaign),
        ["utm_term"] = nameof(UtmTerm),
        ["utm_content"] = nameof(UtmContent),
    };

    public static string HostName { get; set; } = "";
    public static string QueryString
    {
        get => queryString;
        set
        {
            queryString = value ?? "";
            SyncUtmFields();
        }
    }

    public static string UtmSource { get; private set; } = "None";
    public static string UtmMedium { get; private set; } = "None";
    public static string UtmCampaign { get; private set; } = "None";
    public static string UtmTerm { get; private set; } = "None";
    public static string UtmContent { get; private set; } = "None";

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

    private static void SyncUtmFields()
    {
        UtmSource = "None";
        UtmMedium = "None";
        UtmCampaign = "None";
        UtmTerm = "None";
        UtmContent = "None";

        if (string.IsNullOrWhiteSpace(queryString)) return;
        var query = queryString.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 0 || UtmParameterNames.TryGetValue(Uri.UnescapeDataString(pair[0]), out var fieldName) == false) continue;
            var value = SanitizeUtmValue(pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "");
            if (fieldName == nameof(UtmSource)) UtmSource = value;
            if (fieldName == nameof(UtmMedium)) UtmMedium = value;
            if (fieldName == nameof(UtmCampaign)) UtmCampaign = value;
            if (fieldName == nameof(UtmTerm)) UtmTerm = value;
            if (fieldName == nameof(UtmContent)) UtmContent = value;
        }
    }

    private static string SanitizeUtmValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "None";
        value = value.Trim();
        if (value.Length > MaxUtmValueLength) value = value.Substring(0, MaxUtmValueLength);
        return IsSafeTelemetryString(value) ? value : "Invalid";
    }

    private static bool IsSafeTelemetryString(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var ch in value)
        {
            if (ch == '|' || ch == '=') return false;
            if (char.IsLetterOrDigit(ch)) continue;
            if (ch == ' ' || ch == '_' || ch == '-' || ch == ':' || ch == '/' || ch == '\'' || ch == '(' || ch == ')' || ch == '.' || ch == '+') continue;
            return false;
        }
        return true;
    }
}

public sealed record BrowserOverlayRequest(string Id, string? Title = null, string? Message = null);
