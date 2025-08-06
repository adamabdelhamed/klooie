using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class BeatGridBackground : ProtectedConsolePanel, IObservableObject
{
    private readonly Viewport viewport;
    private readonly RGB lightColor;
    private readonly RGB darkColor;
    private readonly RGB darkFocusColor;
    private Func<bool> hasFocus;
    public int CurrentOffset { get; set; }
    public BeatGridBackground(int currentOffset, Viewport viewport, RGB lightColor, RGB darkColor, RGB darkFocusColor, Func<bool> hasFocus)
    {
        this.viewport = viewport;
        this.lightColor = lightColor;
        this.darkColor = darkColor;
        this.CurrentOffset = currentOffset;
        this.darkFocusColor = darkFocusColor;
        this.hasFocus = hasFocus;
        CanFocus = false;
        ZIndex = -1; // Always behind everything else
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        int rows = Height / viewport.RowHeightChars;
        for (int i = 0; i < rows; i++)
        {
            var bgColor = (i + CurrentOffset) % 2 == 0 ? lightColor : hasFocus() ? darkFocusColor : darkColor;
            context.FillRect(bgColor, 0, i * viewport.RowHeightChars, Width, viewport.RowHeightChars);
        }
    }
}
