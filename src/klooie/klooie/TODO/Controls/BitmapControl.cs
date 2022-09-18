namespace klooie;
/// <summary>
/// A control that displays a ConsoleBitmap
/// </summary>
public class BitmapControl : ConsoleControl
{
    /// <summary>
    /// Gets or sets the horizontal offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public int OffsetX { get; set; }

    /// <summary>
    /// Gets or sets the vertical offset to apply when deciding which pixels to draw from the bitmap
    /// </summary>
    public int OffsetY { get; set; }

    /// <summary>
    /// The Bitmap image to render in the control
    /// </summary>
    public ConsoleBitmap Bitmap { get { return Get<ConsoleBitmap>(); } set { Set(value); } }

    /// <summary>
    /// If true then this control will auto size itself based on its target bitmap
    /// </summary>
    public bool AutoSize { get { return Get<bool>(); } set { Set(value); } }

    /// <summary>
    /// Creates a new Bitmap control
    /// </summary>
    public BitmapControl()
    {
        this.Subscribe(nameof(AutoSize), BitmapOrAutoSizeChanged, this);
        this.Subscribe(nameof(Bitmap), BitmapOrAutoSizeChanged, this);
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
        for (var x = 0; x < Bitmap.Width && x < this.Width; x++)
        {
            for (var y = 0; y < Bitmap.Height && y < this.Height; y++)
            {
                var bmpX = x + OffsetX;
                var bmpY = y + OffsetY;

                if (bmpX < 0 || bmpX >= Bitmap.Width || bmpY < 0 || bmpY >= Bitmap.Height) continue;

                ref var pixel = ref Bitmap.GetPixel(x+ OffsetX, y+OffsetY);
                context.DrawPoint(pixel, x, y);
            }
        }
    }
}