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
        var label = GetBeatLabel();
        ctx.DrawString(label.ToConsoleString(Foreground,Background), 0, 0);
    }

    private string GetBeatLabel()
    {
        double duration = Note.DurationBeats;
        int wholeBeats = (int)duration;
        double fractionalBeats = duration - wholeBeats;

        string fractionLabel = GetFractionLabel(fractionalBeats);

        if (wholeBeats > 0 && fractionLabel != null)
            return $"{wholeBeats} {fractionLabel}";
        else if (wholeBeats > 0)
            return $"{wholeBeats}";
        else if (fractionLabel != null)
            return fractionLabel;
        else
            return duration.ToString("0.###");
    }

    // Helper: Converts fractional part to a label like "1/2", "3/4", "1/3", etc.
    private static string GetFractionLabel(double value)
    {
        // Map of common fractions
        var fractions = new (double val, string label)[]
        {
        (0.5,   "1/2"),
        (0.25,  "1/4"),
        (0.75,  "3/4"),
        (0.333, "1/3"),
        (0.667, "2/3"),
        (0.2,   "1/5"),
        (0.4,   "2/5"),
        (0.6,   "3/5"),
        (0.8,   "4/5"),
        (0.125, "1/8"),
        (0.375, "3/8"),
        (0.625, "5/8"),
        (0.875, "7/8"),
        (0.1,   "1/10"),
        (0.3,   "3/10"),
        (0.7,   "7/10"),
        (0.9,   "9/10"),
        (1.0/12, "1/12"),
        (1.0/16, "1/16"),
        (1.0/32, "1/32"),
        };

        const double eps = 0.01; // Acceptable margin for floating point

        foreach (var (val, label) in fractions)
            if (Math.Abs(value - val) < eps)
                return label;

        return null;
    }
}