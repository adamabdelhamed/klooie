using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class NoteCell : ConsoleControl
{
    public readonly NoteExpression Note;
    public NoteCell(NoteExpression note)
    {
        (Note, CanFocus) = (note, false);
        Foreground = RGB.Black;
    }

    // Each cell already knows its bg/fg when created
    protected override void OnPaint(ConsoleBitmap ctx)
    {
        ctx.FillRect(Background, 0, 0, Width, Height);

    }
}