using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class MidiNoteHelper
{
    // Only created once
    private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly (string DisplayString, bool IsWhite)[] NoteCache;

    static MidiNoteHelper()
    {
        // MIDI is 0–127 (sometimes up to 255, but 0–127 is standard)
        NoteCache = new (string, bool)[128];
        for (int midi = 0; midi < NoteCache.Length; midi++)
        {
            int n = midi % 12;
            bool isWhite = Names[n].Length == 1;
            int octave = midi / 12;
            // Example: "60: C4"
            string disp = $"{midi}: {Names[n]}{octave}";
            NoteCache[midi] = (disp, isWhite);
        }
    }

    public static (string DisplayString, bool IsWhite) NoteName(int midi)
    {
        if (midi < 0 || midi >= NoteCache.Length) throw new ArgumentOutOfRangeException(nameof(midi), "MIDI note out of range (0-127)");
        return NoteCache[midi];
    }
}
