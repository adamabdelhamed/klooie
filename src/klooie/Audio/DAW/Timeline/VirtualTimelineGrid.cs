using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class VirtualTimelineGrid : ProtectedConsolePanel
{
    public const double MaxBeatsPerColumn = 1.0;     // each cell is 1 beat (max zoomed out)
    public const double MinBeatsPerColumn = 1.0 / 128; // each cell is 1/8 beat (max zoomed in)

    public Event<ConsoleString> StatusChanged { get; } = Event<ConsoleString>.Create();
    public TimelineViewport Viewport { get; private init; }
    
    public ListNoteSource Notes { get; private set; }
    private readonly Dictionary<NoteExpression, NoteCell> live = new();
    private HashSet<NoteExpression> visibleNow = new HashSet<NoteExpression>();
    private AlternatingBackgroundGrid backgroundGrid;
    public const int ColWidthChars = 1;
    public const int RowHeightChars = 1;
    private Recyclable? focusLifetime;
    public TimelinePlayer TimelinePlayer { get; }
    public Event<double> PlaybackStarting { get; } = Event<double>.Create();
    public MelodyPlayer AudioPlayer { get; private set; }
    private double beatsPerColumn = 1/8.0;

    public TimelineEditor Editor { get; }
    
    public List<NoteExpression> SelectedNotes { get; private set; } = new();

    public InstrumentExpression? Instrument { get; set; } = new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };

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
                if (CurrentMode is SelectionMode sm)  sm.SyncCursorToCurrentZoom();
            }
        }
    }

    public double CurrentBeat => TimelinePlayer.CurrentBeat;
    public double MaxBeat => maxBeat;
    private double maxBeat;
    private Dictionary<string, RGB> instrumentColorMap = new();
    private readonly TimelineInputMode[] userCyclableModes;

    public WorkspaceSession Session { get; private init; }

    public TimelineInputMode CurrentMode { get; private set; }
    public Event<TimelineInputMode> ModeChanging { get; } = Event<TimelineInputMode>.Create();
    public VirtualTimelineGrid(WorkspaceSession session, ListNoteSource? notes = null)
    {
        this.Session = session;
        notes = notes ?? new ListNoteSource();
        this.userCyclableModes =  [new NavigationMode() { Timeline = this }, new SelectionMode() { Timeline = this }];
        Viewport = new TimelineViewport(this);
        TimelinePlayer = new TimelinePlayer(this, () => maxBeat, notes?.BeatsPerMinute ?? 60);

        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220))).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);
        LoadNotes(notes);
        AudioPlayer = new MelodyPlayer(this.Notes);
        TimelinePlayer.BeatChanged.Subscribe(this, static (me, b) => me.RefreshVisibleSet(), this);  
        CurrentMode = this.userCyclableModes[0];
        Editor = new TimelineEditor(session.Commands) { Timeline = this };
        TimelinePlayer.Playing.Subscribe(this, static (me) =>
        {
            var autoStopSuffix = me.TimelinePlayer.StopAtEnd ? " (auto-stop)" : "";
            me.StatusChanged.Fire(ConsoleString.Parse($"[White]Playing... {autoStopSuffix}"));
        }, this);
        TimelinePlayer.Stopped.Subscribe(this, static (me) => me.StatusChanged.Fire(ConsoleString.Parse("[White]Stopped.")), this);
        RefreshVisibleSet();
    }

    public void SetMode(TimelineInputMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ModeChanging.Fire(mode);
        CurrentMode.Enter();
    }

    public void NextMode()
    {
        int i = Array.IndexOf(userCyclableModes, CurrentMode);
        SetMode(userCyclableModes[(i + 1) % userCyclableModes.Length]);
    }

    private void LoadNotes(ListNoteSource notes)
    {
        this.Notes = notes;
        Viewport.FirstVisibleMidi = notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(TimelineViewport.DefaultFirstVisibleMidi).Min();
        maxBeat = notes.Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty(0).Max();
        var instruments = notes.Where(n => n.Instrument != null).Select(n => n.Instrument.Name).Distinct().ToArray();
        var instrumentColors = instruments.Select((s, i) => GetInstrumentColor(i)).ToArray();
        for (int i = 0; i < instruments.Length; i++)
        {
            instrumentColorMap[instruments[i]] = instrumentColors[i];
        }
        AudioPlayer = new MelodyPlayer(this.Notes);
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

    private Recyclable? playLifetime;
    public void StartPlayback()
    {
        if(TimelinePlayer.IsPlaying) return;
        ConsoleApp.Current.Scheduler.Delay(60, () =>
        {
            TimelinePlayer.Start(CurrentBeat);
            PlaybackStarting.Fire(CurrentBeat);
        });
        playLifetime?.TryDispose();
        playLifetime = DefaultRecyclablePool.Instance.Rent();
        AudioPlayer?.PlayFrom(CurrentBeat, playLifetime);
    }

    public void StopPlayback() => TimelinePlayer.Stop();

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
    }

    private void UpdateAlternatingBackgroundOffset() => backgroundGrid.CurrentOffset = ConsoleMath.Round(Viewport.FirstVisibleMidi / (double)RowHeightChars);

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
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Key == ConsoleKey.Spacebar)
            {
                if (TimelinePlayer.IsPlaying)
                {
                    playLifetime?.Dispose();
                    playLifetime = null;
                    TimelinePlayer.Pause();
                }
                else
                {
                    StartPlayback();
                }
            }
            else if (k.Key == ConsoleKey.OemPlus || k.Key == ConsoleKey.Add)
            {
                if (BeatsPerColumn / 2 >= MinBeatsPerColumn)
                    BeatsPerColumn /= 2; // zoom in
            }
            else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract)
            {
                if (BeatsPerColumn * 2 <= MaxBeatsPerColumn)
                    BeatsPerColumn *= 2; // zoom out
            }
            else if (k.Key == ConsoleKey.M)
            {
                NextMode();  
            }
            else if (!Editor.HandleKeyInput(k))
            {
                CurrentMode.HandleKeyInput(k);
            }
        }, focusLifetime);
    }

    public void RefreshVisibleSet()
    {
        if(live.Count == 0 && Notes.Count > 0)
        {
            Viewport.FirstVisibleMidi = Math.Max(0, Notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(TimelineViewport.DefaultFirstVisibleMidi).Min() - 12);
        }
        maxBeat = Notes.Select(n => n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : GetSustainedNoteDurationBeats(n))).DefaultIfEmpty(0).Max();
        double beatStart = Viewport.FirstVisibleBeat;
        double beatEnd = beatStart + Viewport.BeatsOnScreen;
        int midiTop = Viewport.FirstVisibleMidi;
        int midiBot = midiTop + Viewport.MidisOnScreen;

        // Track visible notes this frame
        visibleNow.Clear();

        for (int i = 0; i < Notes.Count; i++)
        {
            var note = Notes[i];
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
                cell = ProtectedPanel.Add(new NoteCell(note) { ZIndex = 1 });
                live[note] = cell;
            }

            cell.Background = SelectedNotes.Contains(note) ? SelectionMode.SelectedNoteColor : 
                note.Instrument == null ? RGB.Orange : instrumentColorMap.TryGetValue(note.Instrument.Name, out var color) ? color 
                : RGB.Orange;

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

        Editor.PositionAddNotePreview();
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
        // Show duration from note's start to current playhead position
        double sustainedBeats = TimelinePlayer.CurrentBeat - n.StartBeat;
        return Math.Max(0, sustainedBeats);
    }
    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    protected override void OnReturn()
    {
        base.OnReturn();
        playLifetime?.TryDispose();
        playLifetime = null;
    }
}
