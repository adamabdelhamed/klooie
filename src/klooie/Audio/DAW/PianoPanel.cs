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
            var bg = isWhite ? RGB.White : RGB.Black;
            var fg = isWhite ? RGB.Black : RGB.White;

            ctx.FillRect(bg, 0, i, KeyWidth, 1);

            var leftOffSetToCenter = (KeyWidth - noteName.Length) / 2;
            ctx.DrawString(noteName, fg, bg, leftOffSetToCenter, i);
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
