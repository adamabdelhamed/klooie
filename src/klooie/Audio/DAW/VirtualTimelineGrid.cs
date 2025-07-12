using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class VirtualTimelineGrid : ProtectedConsolePanel
{
    public TimelineViewport Viewport { get; private init; }
    private INoteSource notes;
    private readonly Dictionary<NoteExpression, NoteCell> live = new();
    private AlternatingBackgroundGrid backgroundGrid;
    private const int ColWidthChars = 1;
    private const int RowHeightChars = 1;
    private Recyclable? focusLifetime;

    private double beatsPerColumn = 1/8.0; 
    public double BeatsPerColumn
    {
        get => beatsPerColumn;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(BeatsPerColumn));
            if (Math.Abs(beatsPerColumn - value) > 0.0001)
            {
                beatsPerColumn = value;
                UpdateViewportBounds(); 
                RefreshVisibleSet();
            }
        }
    }

    public double CurrentBeat { get; private set; } = 0; 
    private long? playbackStartTimestamp = null;
    private double playheadStartBeat = 0;
    private bool isPlaying = false;

    private double maxBeat;
    private Dictionary<string, RGB> instrumentColorMap = new();
    public VirtualTimelineGrid(INoteSource notes)
    {
        Viewport = new TimelineViewport();
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220))).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);
        LoadNotes(notes);
    }

    private void LoadNotes(INoteSource? notes)
    {
        this.notes = notes;
        if (notes == null)
        {
            maxBeat = 0;
            instrumentColorMap = new Dictionary<string, RGB>();
            return;
        }
        Viewport.FirstVisibleMidi = notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(TimelineViewport.DefaultFirstVisibleMidi).Min();
        maxBeat = notes.Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty(0).Max();
        var instruments = notes.Where(n => n.Instrument != null).Select(n => n.Instrument.Name).Distinct().ToArray();
        var instrumentColors = instruments.Select((s, i) => GetInstrumentColor(i)).ToArray();
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
        double secondsPerBeat = 60.0 / notes?.BeatsPerMinute ?? 60;
        double beat = playheadStartBeat + elapsedSeconds / secondsPerBeat;

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
        int x = ConsoleMath.Round(relBeat / BeatsPerColumn) * ColWidthChars;

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
        Viewport.BeatsOnScreen = Math.Max(1, Width * BeatsPerColumn / ColWidthChars);
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
            else if (k.Key == ConsoleKey.OemPlus || k.Key == ConsoleKey.Add) { BeatsPerColumn /= 2; }  // Zoom in (twice as detailed)
            else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract) { BeatsPerColumn *= 2; } // Zoom out (twice as broad)
            else return; // Not handled
            RefreshVisibleSet();
        }, focusLifetime);
    }

    HashSet<NoteExpression> visibleNow = new HashSet<NoteExpression>();
    public void RefreshVisibleSet()
    {
        if(live.Count == 0 && notes.Count > 0)
        {
            Viewport.FirstVisibleMidi = Math.Max(0, notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(TimelineViewport.DefaultFirstVisibleMidi).Min() - 12);
        }
        double beatStart = Viewport.FirstVisibleBeat;
        double beatEnd = beatStart + Viewport.BeatsOnScreen;
        int midiTop = Viewport.FirstVisibleMidi;
        int midiBot = midiTop + Viewport.MidisOnScreen;

        // Track visible notes this frame
        visibleNow.Clear();

        for (int i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (note.Velocity == 0) continue;

            double durBeats = note.DurationBeats >= 0 ? note.DurationBeats : GetSustainedNoteDurationBeats(note);
            bool isVisible =
                (note.StartBeat + durBeats >= beatStart) &&
                (note.StartBeat <= beatEnd) &&
                (note.MidiNote >= midiTop) &&
                (note.MidiNote <= midiBot);

            if (!isVisible) continue;
            visibleNow.Add(note);

            if (!live.TryGetValue(note, out NoteCell cell))
            {
                cell = ProtectedPanel.Add(new NoteCell(note));
                cell.Background = note.Instrument == null ? RGB.Orange : instrumentColorMap.TryGetValue(note.Instrument.Name, out var color) ? color : RGB.Orange;
                live[note] = cell;
            }
            // Always re-position/re-size every visible note
            PositionCell(cell);
        }

        // Remove cells that are no longer visible
        foreach (var kvp in live.ToArray())
        {
            if (!visibleNow.Contains(kvp.Key))
            {
                kvp.Value.Dispose();
                live.Remove(kvp.Key);
            }
        }
    }

    private void PositionCell(NoteCell cell)
    {
        // convert beat/midi → chars
        double beatsFromLeft = cell.Note.StartBeat - Viewport.FirstVisibleBeat;

        int x = ConsoleMath.Round((cell.Note.StartBeat - Viewport.FirstVisibleBeat) / BeatsPerColumn) * ColWidthChars;
        int y = (Viewport.FirstVisibleMidi + Viewport.MidisOnScreen - 1 - cell.Note.MidiNote) * RowHeightChars;

        double durBeats = cell.Note.DurationBeats >= 0 ? cell.Note.DurationBeats : GetSustainedNoteDurationBeats(cell.Note);
        int w = (int)Math.Max(1, ConsoleMath.Round(durBeats / BeatsPerColumn) * ColWidthChars);
        int h = RowHeightChars;

        cell.MoveTo(x, y);
        cell.ResizeTo(w, h);
    }

    private double GetSustainedNoteDurationBeats(NoteExpression n)
    {
        var totalPlayTime = Stopwatch.GetElapsedTime(((ListNoteSource)notes).StartTimestamp.Value);
        var thisNoteStart = n.StartBeat * 60.0 / notes.BeatsPerMinute;
        var thisNoteSustained = totalPlayTime.TotalSeconds - thisNoteStart;
        var thisNoteSustainedBeats = thisNoteSustained * notes.BeatsPerMinute / 60.0;
        return Math.Max(0, thisNoteSustainedBeats);
    }
}
