using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal readonly struct Run
{
    public readonly int Start;
    public readonly int Length;
    public readonly RGB FG;
    public readonly RGB BG;
    public readonly bool Underlined;

    public Run(int start, int length, RGB fg, RGB bg, bool underlined)
    {
        Start = start;
        Length = length;
        FG = fg;
        BG = bg;
        Underlined = underlined;
    }
}

internal struct AnsiState
{
    public RGB Fg, Bg;
    public bool Under;
    public int CursorX, CursorY;
    public bool HasColor; // first-run guard
}