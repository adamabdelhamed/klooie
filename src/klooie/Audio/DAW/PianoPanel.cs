using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class PianoPanel : ProtectedConsolePanel
{
    public const int KeyWidth = 11;
    private readonly TimelineViewport vp;

    // Optionally: let user customize width
    public PianoPanel(TimelineViewport viewport)
    {
        vp = viewport;
        CanFocus = false;
        Background = new RGB(240, 240, 240); // match grid
        viewport.SubscribeToAnyPropertyChange(this, _ => Refresh(), this);
    }

    public void Refresh()
    {
        Height = vp.MidisOnScreen;
    }

    protected override void OnPaint(ConsoleBitmap ctx)
    {
        int midiTop = vp.FirstVisibleMidi;
        for (int i = 0; i < vp.MidisOnScreen; i++)
        {
            int midi = midiTop + (vp.MidisOnScreen - 1 - i); // top to bottom

            var (noteName, isWhite) = NoteName(midi);

            if (isWhite)
            {
                // Regular white key: fill entire width
                ctx.FillRect(RGB.White, 0, i, KeyWidth, 1);
                ctx.DrawString(
                    noteName,
                    RGB.Black, RGB.White,
                    (KeyWidth - noteName.Length) / 2,
                    i
                );
            }
            else
            {
                // Black key with white at right edge (last 2 cells)
                int blackWidth = KeyWidth - 2;
                if (blackWidth > 0)
                    ctx.FillRect(RGB.Black, 0, i, blackWidth, 1);

                // White "bottom" on the right
                ctx.FillRect(RGB.White, blackWidth, i, 2, 1);

                // Draw note name centered in the black area
                int leftOffSetToCenter = (blackWidth - noteName.Length) / 2;
                if (leftOffSetToCenter >= 0)
                {
                    ctx.DrawString(
                        noteName,
                        RGB.White, RGB.Black,
                        leftOffSetToCenter,
                        i
                    );
                }
            }
        }
    }


    private static (string, bool) NoteName(int midi)
    {
        // 0 = C, 1 = C#, 2 = D, ... 11 = B
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int n = midi % 12;
        bool isWhite = names[n].Length == 1;
        // Example: "C4"
        int octave = (midi / 12) - 1;
        return ($"{midi}: {names[n]}{octave}", isWhite);
    }
}
