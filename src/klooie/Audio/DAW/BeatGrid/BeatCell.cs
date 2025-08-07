using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class BeatCell<T> : ConsoleControl
{
    public readonly T Value;
    public BeatCell(T value)
    {
        (Value, CanFocus) = (value, false);
        Foreground = RGB.Black;
    }


    private static readonly RGB[] BaseTrackColors = new[]
    {
        new RGB(220, 60, 60),
        new RGB(60, 180, 90),
        new RGB(65, 105, 225),
        new RGB(240, 200, 60),
        new RGB(200, 60, 200),
        new RGB(50, 220, 210),
        new RGB(245, 140, 30),
    };

    private static readonly float[] PaleFractions = new[]
    {
        0.0f,
        0.35f,
        0.7f,
    };

    public static RGB GetColor(int index)
    {
        int baseCount = BaseTrackColors.Length;
        int shade = index / baseCount;
        int colorIdx = index % baseCount;
        float pale = PaleFractions[Math.Min(shade, PaleFractions.Length - 1)];
        RGB color = BaseTrackColors[colorIdx];
        return color.ToOther(RGB.White, pale);
    }
}