namespace klooie;
/// <summary>
/// An interfaced that, when implemented, allows you
/// to edit the freshly painted bitmap of a control 
/// just before it is composed onto its parent
/// </summary>
public interface IConsoleControlFilter
{
    /// <summary>
    /// The control that was just painted
    /// </summary>
    ConsoleControl Control { get; set; }

    /// <summary>
    /// The filter implementation
    /// </summary>
    /// <param name="bitmap">The bitmap you can modify</param>
    void Filter(ConsoleBitmap bitmap);
}