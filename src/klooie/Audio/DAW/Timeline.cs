using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

// Generator makes observables
public partial class TimelineViewport : IObservableObject
{
    public partial int FirstVisibleMidi { get; set; }
    public partial int MidisOnScreen { get; set; }             
    public partial double FirstVisibleBeat { get; private set; }
    public partial double BeatsOnScreen { get; set; }
    public void ScrollRows(int delta) => FirstVisibleMidi = Math.Clamp(FirstVisibleMidi + delta, 0, 127);
    public void ScrollBeats(double dx) => FirstVisibleBeat = Math.Max(0, FirstVisibleBeat + dx);
}

public class NoteCell : ConsoleControl
{
    public readonly NoteExpression Note;
    public NoteCell(NoteExpression note)
    {
        (Note, CanFocus) = (note, false);
        Background = RGB.Orange;
    }

    // Each cell already knows its bg/fg when created
    protected override void OnPaint(ConsoleBitmap ctx)
    {
        ctx.FillRect(Background, 0, 0, Width, Height);
    }
}

public partial class AlternatingBackgroundGrid : ProtectedConsolePanel, IObservableObject
{
    private readonly int rowHeight;
    private readonly RGB lightColor;
    private readonly RGB darkColor;
    public partial int CurrentOffset { get; set; }
    public AlternatingBackgroundGrid(int currentOffset, int rowHeight, RGB lightColor, RGB darkColor)
    {
        this.rowHeight = rowHeight;
        this.lightColor = lightColor;
        this.darkColor = darkColor;
        this.CurrentOffset = currentOffset;
        CanFocus = false;
        ZIndex = -1; // Always behind everything else
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        int rows = Height / rowHeight;
        for (int i = 0; i < rows; i++)
        {
            var bgColor = (i + CurrentOffset) % 2 == 0 ? lightColor : darkColor;
            context.FillRect(bgColor, 0, i * rowHeight, Width, rowHeight);
        }
    }
}

public class VirtualTimelineGrid : ProtectedConsolePanel
{
    public TimelineViewport Viewport { get; private init; }
    private readonly Song song;
    private readonly Dictionary<NoteExpression, NoteCell> live = new();
    private AlternatingBackgroundGrid backgroundGrid;
    // Tune these: visual char size of one tick / one midi row
    private const int ColWidthChars = 3;
    private const int RowHeightChars = 1;
    private const int TicksPerBeat = 96;
    private Recyclable? focusLifetime;
    public VirtualTimelineGrid(Song s)
    {
        song = s;
        Viewport = new TimelineViewport();
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Viewport.FirstVisibleMidi = s.Notes.Notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(0).Min();
        this.Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220))).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);
    }

    private void UpdateAlternatingBackgroundOffset()
    {
        backgroundGrid.CurrentOffset = ConsoleMath.Round(Viewport.FirstVisibleMidi / (double)RowHeightChars);
    }

    private void UpdateViewportBounds()
    {
        Viewport.BeatsOnScreen = Math.Max(1, Width / ColWidthChars);
        Viewport.MidisOnScreen = Math.Max(1, Height / RowHeightChars);
    }

    public void EnableKeyboardInput()
    {
        focusLifetime?.TryDispose();
        focusLifetime = DefaultRecyclablePool.Instance.Rent();
        Unfocused.SubscribeOnce(() => focusLifetime.TryDispose());
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(k =>
        {
            if (k.Key == ConsoleKey.LeftArrow || k.Key == ConsoleKey.A) Viewport.ScrollBeats(-1);
            else if (k.Key == ConsoleKey.RightArrow || k.Key == ConsoleKey.D) Viewport.ScrollBeats(+1);
            else if (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.W) Viewport.ScrollRows(+1);
            else if (k.Key == ConsoleKey.DownArrow || k.Key == ConsoleKey.S) Viewport.ScrollRows(-1);
            else if (k.Key == ConsoleKey.PageUp) Viewport.ScrollRows(+12);
            else if (k.Key == ConsoleKey.PageDown) Viewport.ScrollRows(-12);
            else if (k.Key == ConsoleKey.Home) Viewport.FirstVisibleMidi = 0;
            else if (k.Key == ConsoleKey.End) Viewport.FirstVisibleMidi = 127;
            else return; // Not handled
            RefreshVisibleSet();
        }, focusLifetime);
    }
    

    public void RefreshVisibleSet()
    {
        // 1. Mark which notes *should* be on screen
        double beatStart = Viewport.FirstVisibleBeat;
        double beatEnd = beatStart + Viewport.BeatsOnScreen;
        int midiTop = Viewport.FirstVisibleMidi;
        int midiBot = midiTop + Viewport.MidisOnScreen;


        // 2. Add newly-visible notes
        for (int i = 0; i < song.Notes.Notes.Count; i++)
        {
            var note = song.Notes.Notes[i];
            if(note.Velocity == 0) continue; // Skip silent notes
            if (live.TryGetValue(note, out NoteCell cell))
            {
                PositionCell(cell, note);
            }
            else
            {
                cell = ProtectedPanel.Add(new NoteCell(note));
                PositionCell(cell, note);
                live[note] = cell;
            }
        }

        // 3. Remove now-invisible notes
        foreach (var kvp in live.ToArray())
        {
            var note = kvp.Key;
            if (note.StartBeat + note.DurationBeats < beatStart || note.StartBeat > beatEnd ||
                note.MidiNote < midiTop || note.MidiNote > midiBot)
            {
                kvp.Value.Dispose();
                live.Remove(note);
            }
        }
    }

    private void PositionCell(NoteCell cell, NoteExpression n)
    {
        // convert beat/midi → chars
        double beatsFromLeft = n.StartBeat - Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(beatsFromLeft * ColWidthChars);

        int y = (Viewport.FirstVisibleMidi + Viewport.MidisOnScreen - 1 - n.MidiNote) * RowHeightChars;

        double durBeats = n.DurationBeats;
        int w = (int)Math.Max(1, ConsoleMath.Round(durBeats * ColWidthChars));
        int h = RowHeightChars;

        cell.MoveTo(x, y);
        cell.ResizeTo(w, h);
    }
}

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
        viewport.SubscribeToAnyPropertyChange(this, _=> Refresh(), this);
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

public class PianoWithTimeline : ProtectedConsolePanel
{
    private GridLayout layout;

    public PianoPanel Piano { get; private init; }
    public VirtualTimelineGrid Timeline { get; private init; }
    public PianoWithTimeline(Song song)
    {
        layout = ProtectedPanel.Add(new GridLayout("100%", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Timeline = layout.Add(new VirtualTimelineGrid(song), 1, 0); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Timeline.Viewport), 0, 0);
    }
}
