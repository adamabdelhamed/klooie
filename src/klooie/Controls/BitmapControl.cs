namespace klooie;
/// <summary>
/// A control that displays a ConsoleBitmap
/// </summary>
public partial class BitmapControl : ConsoleControl
{
    /// <summary>
    /// Gets or sets the horizontal offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public partial int OffsetX { get; set; }

    /// <summary>
    /// Gets or sets the vertical offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public partial int OffsetY { get; set; }

    /// <summary>
    /// The Bitmap image to render in the control
    /// </summary>
    public partial ConsoleBitmap Bitmap { get; set; }

    /// <summary>
    /// If true then this control will auto size itself based on its target bitmap
    /// </summary>
    public partial bool AutoSize { get; set; }

    /// <summary>
    /// Creates a new Bitmap control
    /// <param name="initialValue">the bitmap to draw</param>
    /// </summary>
    public BitmapControl(ConsoleBitmap initialValue = null)
    {
        AutoSizeChanged.Subscribe( BitmapOrAutoSizeChanged, this);
        BitmapChanged.Subscribe(BitmapOrAutoSizeChanged, this);
        OffsetXChanged.Subscribe(BitmapOrAutoSizeChanged, this);
        OffsetYChanged.Subscribe(BitmapOrAutoSizeChanged, this);
        Bitmap = initialValue;
    }

    private void BitmapOrAutoSizeChanged()
    {
        if (AutoSize && Bitmap != null)
        {
            this.Width = Bitmap.Width;
            this.Height = Bitmap.Height;
            Application?.RequestPaint();
        }
    }

    /// <summary>
    /// Draws the bitmap
    /// </summary>
    /// <param name="context">the pain context</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        if (Bitmap == null) return;
        context.DrawBitmap(Bitmap, OffsetX, OffsetY);
    }
}