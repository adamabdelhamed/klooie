using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class PianoPanel : ProtectedConsolePanel
{
    public const int KeyWidth = 11;
    private readonly Viewport vp;

    // Optionally: let user customize width
    public PianoPanel(Viewport viewport)
    {
        vp = viewport;
        CanFocus = false;
        Background = new RGB(240, 240, 240); // match grid
        viewport.Changed.Subscribe(this, _ => Refresh(), this);
    }

    public void Refresh()
    {
        Height = vp.RowsOnScreen;
    }

    protected override void OnPaint(ConsoleBitmap ctx)
    {
        for (int i = 0; i < vp.RowsOnScreen; i++)
        {
            int midi = 127 - (vp.FirstVisibleRow + i);
            if (midi < 0 || midi > 127)
                continue;

            var (noteName, isWhite) = MidiNoteHelper.NoteName(midi);

            if (isWhite)
            {
                ctx.FillRect(RGB.White, 0, i, KeyWidth, 1);
                ctx.DrawString(noteName, RGB.Black, RGB.White, 0, i);
            }
            else
            {
                int blackWidth = KeyWidth - 3;
                if (blackWidth > 0) ctx.FillRect(RGB.Black, 0, i, blackWidth, 1);

                ctx.FillRect(RGB.White, blackWidth, i, 3, 1);
                ctx.DrawString(noteName, RGB.White, RGB.Black, 0, i);
            }
        }
    }

}
