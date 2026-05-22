namespace klooie.blazor.BrowserConsole;

public sealed class BrowserKeyboardEvent
{
    public string Key { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Location { get; set; }
    public bool AltKey { get; set; }
    public bool ShiftKey { get; set; }
    public bool CtrlKey { get; set; }
    public bool MetaKey { get; set; }
    public bool CapsLock { get; set; }
}
