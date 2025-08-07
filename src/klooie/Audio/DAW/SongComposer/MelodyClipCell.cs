using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MelodyClipCell : BeatCell<MelodyClip>
{
    private ComposerTrack track;
    private BeatGrid<MelodyClip> grid;
    private Viewport viewport;
    public MelodyClipCell(MelodyClip value, BeatGrid<MelodyClip> grid, Viewport vp) : base(value) 
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        track = WorkspaceSession.Current.CurrentSong.Tracks.FirstOrDefault(t => t.Melodies.Contains(value)) 
                ?? throw new ArgumentException("MelodyClip does not belong to any track.", nameof(value));
        viewport = vp ?? throw new ArgumentNullException(nameof(vp));
    }
    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);

        var y = 1;
        for(var x = 0; x < Width; x++)
        {
            var beatAtStartOfPixel = Value.StartBeat + (double)x * grid.BeatsPerColumn / viewport.ColWidthChars;
            var beatAtEndOfPixel = Value.StartBeat + (double)(x + 1) * grid.BeatsPerColumn / viewport.ColWidthChars;
            var notesAtBeat = 0;
            for(var i = 0; i < Value.Melody.Count; i++)
            {
                var note = Value.Melody[i];
                var noteStart = Value.StartBeat + note.StartBeat;
                var noteEnd = noteStart + note.DurationBeats;
                if (noteStart < beatAtEndOfPixel && noteEnd > beatAtStartOfPixel)
                {
                    notesAtBeat++;
                }
            }
            var displayChar = notesAtBeat == 0 ? ' ' : notesAtBeat < 10 ? (char)('0' + notesAtBeat) : '+';
            context.DrawPoint(new ConsoleCharacter(displayChar, Background.Darker, Background), x, y);
        }

        context.DrawRect(new ConsoleCharacter('#', Background.Darker, Background), 0, 0, Width, Height);
    }
}
