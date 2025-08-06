using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class PianoPanel : ProtectedConsolePanel
{
    public const int KeyWidth = 11;
    private readonly MelodyComposerViewport vp;

    // Optionally: let user customize width
    public PianoPanel(MelodyComposerViewport viewport)
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
        int midiTop = vp.FirstVisibleRow;
        for (int i = 0; i < vp.RowsOnScreen; i++)
        {
            int midi = midiTop + (vp.RowsOnScreen - 1 - i); // top to bottom
            var (noteName, isWhite) = MidiNoteHelper.NoteName(midi);

            if (isWhite)
            {
                // Regular white key: fill entire width
                ctx.FillRect(RGB.White, 0, i, KeyWidth, 1);
                ctx.DrawString(noteName, RGB.Black, RGB.White, 0, i);
            }
            else
            {
                // Black key with white at right edge
                int blackWidth = KeyWidth - 3;
                if (blackWidth > 0) ctx.FillRect(RGB.Black, 0, i, blackWidth, 1);

                // White "bottom" on the right
                ctx.FillRect(RGB.White, blackWidth, i, 3, 1);
                ctx.DrawString(noteName, RGB.White, RGB.Black, 0, i);
            }
        }
    }
}
