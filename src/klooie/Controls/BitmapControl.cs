namespace klooie;
/// <summary>
/// A control that displays a ConsoleBitmap
/// </summary>
public class BitmapControl : ConsoleControl
{
    /// <summary>
    /// Gets or sets the horizontal offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public int OffsetX { get => Get<int>(); set => Set(value); }

    /// <summary>
    /// Gets or sets the vertical offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public int OffsetY { get => Get<int>(); set => Set(value); }

    /// <summary>
    /// The Bitmap image to render in the control
    /// </summary>
    public ConsoleBitmap Bitmap { get => Get<ConsoleBitmap>(); set => Set(value);  }

    /// <summary>
    /// If true then this control will auto size itself based on its target bitmap
    /// </summary>
    public bool AutoSize { get => Get<bool>();  set => Set(value);  }

    /// <summary>
    /// Creates a new Bitmap control
    /// <param name="initialValue">the bitmap to draw</param>
    /// </summary>
    public BitmapControl(ConsoleBitmap initialValue = null)
    {
        this.Subscribe(nameof(AutoSize), BitmapOrAutoSizeChanged, this);
        this.Subscribe(nameof(Bitmap), BitmapOrAutoSizeChanged, this);
        this.Subscribe(nameof(OffsetX), BitmapOrAutoSizeChanged, this);
        this.Subscribe(nameof(OffsetY), BitmapOrAutoSizeChanged, this);
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