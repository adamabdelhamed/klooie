using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

// Generator makes observables
public partial class TimelineViewport : IObservableObject
{
    public partial int FirstVisibleMidi { get; set; }
    public partial int MidisOnScreen { get; set; }             
    public partial double FirstVisibleBeat { get; set; }
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
    private const int ColWidthChars = 4;
    private const int RowHeightChars = 1;
    private const int TicksPerBeat = 96;
    private Recyclable? focusLifetime;

    public double CurrentBeat { get; private set; } = 0; // Current beat in the timeline, used for playback
    private long? playbackStartTimestamp = null;
    private double playheadStartBeat = 0;
    private bool isPlaying = false;

    private double maxBeat;
    private Dictionary<string, RGB> instrumentColorMap = new();
    public VirtualTimelineGrid(Song s)
    {
        song = s;
        maxBeat = song.Notes.Notes.Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty(0).Max();
        Viewport = new TimelineViewport();
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Viewport.FirstVisibleMidi = s.Notes.Notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(0).Min();
        this.Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220))).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);

        var instruments = song.Notes.Notes.Where(n => n.Instrument != null).Select(n => n.Instrument.Name).Distinct().ToArray();
        var instrumentColors = instruments.Select((s,i) => GetInstrumentColor(i)).ToArray();
        for (int i = 0; i < instruments.Length; i++)
        {
            instrumentColorMap[instruments[i]] = instrumentColors[i];
        }

    }

    private static readonly RGB[] BaseInstrumentColors = new[]
    {
    new RGB(220, 60, 60),    // Red
    new RGB(60, 180, 90),    // Green
    new RGB(65, 105, 225),   // Blue
    new RGB(240, 200, 60),   // Yellow/Gold
    new RGB(200, 60, 200),   // Magenta
    new RGB(50, 220, 210),   // Cyan
    new RGB(245, 140, 30),   // Orange
};

    private static readonly float[] PaleFractions = new[]
    {
        0.0f, // Full color (original)
        0.35f,
        0.7f,
    };

    private RGB GetInstrumentColor(int index)
    {
        int baseCount = BaseInstrumentColors.Length;
        int shade = index / baseCount;
        int colorIdx = index % baseCount;
        float pale = PaleFractions[Math.Min(shade, PaleFractions.Length - 1)];

        // Lerp: (1-pale)*BaseColor + pale*White
        RGB color = BaseInstrumentColors[colorIdx];
        return color.ToOther(RGB.White, pale);
    }

    public void StartPlayback()
    {
        if (isPlaying) return;

        isPlaying = true;
        playheadStartBeat = CurrentBeat;
        playbackStartTimestamp = Stopwatch.GetTimestamp();
        ScheduleNextTick();
    }

    public void StopPlayback()
    {
        isPlaying = false;
        playbackStartTimestamp = null;
    }

    private void ScheduleNextTick()
    {
        if (!isPlaying) return;
        ConsoleApp.Current.Scheduler.Delay(10, () => PlaybackTick());
    }

    private void PlaybackTick()
    {
        if (!isPlaying) return;

        // Calculate elapsed time
        double elapsedSeconds = Stopwatch.GetElapsedTime(playbackStartTimestamp.Value).TotalSeconds;
        double secondsPerBeat = 60.0 / song.BeatsPerMinute;
        double beat = playheadStartBeat + elapsedSeconds / secondsPerBeat;

        // Clamp to song length
        if (beat > maxBeat)
        {
            CurrentBeat = (int)maxBeat;
            StopPlayback();
            return;
        }
        else
        {
            CurrentBeat = beat;
        }

        // Optionally scroll viewport to follow playhead (simple auto-scroll)
        if (CurrentBeat > Viewport.FirstVisibleBeat + Viewport.BeatsOnScreen * 0.8)
        {
            Viewport.FirstVisibleBeat = ConsoleMath.Round(CurrentBeat - Viewport.BeatsOnScreen * 0.2);
            RefreshVisibleSet();
        }
        ScheduleNextTick();
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        double relBeat = CurrentBeat - Viewport.FirstVisibleBeat;
        int x = ConsoleMath.Round(relBeat * ColWidthChars);
        if (x < 0 || x >= Width) return;
        for (int y = 0; y < Height; y++)
        {
            var existingPixel = context.GetPixel(x, y);
            context.DrawString("|".ToRed(existingPixel.BackgroundColor), x, y);
        }
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
                cell.Background = instrumentColorMap.TryGetValue(note.Instrument.Name, out var color) ? color : RGB.Orange;
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

public class StatusBar : ProtectedConsolePanel
{
    public const int Height = 1;

    private ConsoleStringRenderer label;

    public ConsoleString Message
    {
        get => label.Content;
        set => label.Content = value;
    }

    public StatusBar()
    {
        Background = new RGB(50, 50, 50);
        label = ProtectedPanel.Add(new ConsoleStringRenderer("Ready".ToWhite()) { CompositionMode = CompositionMode.BlendBackground }).DockToLeft();
    }
}

public class PianoWithTimeline : ProtectedConsolePanel
{
    private GridLayout layout;
    public PianoPanel Piano { get; private init; }
    public VirtualTimelineGrid Timeline { get; private init; }
    public StatusBar StatusBar { get; private init; }
    public PianoWithTimeline(Song song)
    {
        layout = ProtectedPanel.Add(new GridLayout($"1r;{StatusBar.Height}p", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Timeline = layout.Add(new VirtualTimelineGrid(song), 1, 0); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Timeline.Viewport), 0, 0);
        StatusBar = layout.Add(new StatusBar(), column: 0, row: 1, columnSpan: 2);
    }

    public void StartPlayback()
    {
        Timeline.StartPlayback();
        StatusBar.Message = "Playing...".ToWhite();
    }

    public void StopPlayback()
    {
        Timeline.StopPlayback();
        StatusBar.Message = "Stopped".ToWhite();
    }
}
