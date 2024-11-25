namespace klooie;
/// <summary>
/// A progress bar designed for use with the console bitmap player.  It shows the current play cursor and indicates loading progress
/// </summary>
internal sealed partial class PlayerProgressBar : ConsoleControl
{
    /// <summary>
    /// The current position of the loading indicator (0 to 1)
    /// </summary>
    public partial double LoadProgressPosition { get; set; }

    /// <summary>
    /// The current position of the play cursor (0 to 1)
    /// </summary>
    public partial double PlayCursorPosition { get; set; }

    /// <summary>
    /// The color of the portion of the bar that represents loaded content, defaults to white
    /// </summary>
    public partial RGB LoadingProgressColor { get; set; }

    /// <summary>
    /// True if you want to render the play cursor, false otherwise
    /// </summary>
    public partial bool ShowPlayCursor { get; set; }

    /// <summary>
    /// The color of the play cursor, defaults to green
    /// </summary>
    public partial RGB PlayCursorColor { get; set; }

    public PlayerProgressBar()
    {
        this.Height = 1;
        this.Background = RGB.Gray;
        this.LoadingProgressColor = RGB.White;
        this.PlayCursorColor = RGB.Green;
        this.ShowPlayCursor = true;
        this.CanFocus = false;
    }

    /// <summary>
    /// Paints the progress bar
    /// </summary>
    /// <param name="context"></param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        var loadProgressPixels = (int)(0.5 + (LoadProgressPosition * Width));
        var playCursorOffset = (int)(0.5 + (PlayCursorPosition * Width));
        if (playCursorOffset == Width) playCursorOffset--;

        // draws the loading progress
        context.FillRect(new ConsoleCharacter(' ', null, LoadingProgressColor), 0, 0, loadProgressPixels, 1);

        if (ShowPlayCursor)
        {
            // draws the play cursor
            context.DrawPoint(new ConsoleCharacter(' ', null, PlayCursorColor), playCursorOffset, 0);
        }
    }
}
