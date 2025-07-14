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
    private INoteSource notes;
    public INoteSource NoteSource => notes;
    private readonly Dictionary<NoteExpression, NoteCell> live = new();
    private AlternatingBackgroundGrid backgroundGrid;
    public const int ColWidthChars = 1;
    public const int RowHeightChars = 1;
    private Recyclable? focusLifetime;
    public TimelinePlayer Player { get; }
    public Event<double> PlaybackStarting { get; } = Event<double>.Create();
    public MelodyPlayer AudioPlayer { get; private set; }
    private double beatsPerColumn = 1/8.0;
    
    public List<NoteExpression> SelectedNotes { get; private set; } = new();

    private ConsoleControl? addNotePreview;
    private (double Start, double Duration, int Midi)? pendingAddNote;

    public Func<ISynthPatch> InstrumentFactory { get; set; } = () => null;

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

    public double CurrentBeat => Player.CurrentBeat;
    public double MaxBeat => maxBeat;
    private double maxBeat;
    private Dictionary<string, RGB> instrumentColorMap = new();
    private readonly TimelineInputMode[] userCyclableModes;
    public TimelineInputMode CurrentMode { get; private set; }
    public Event<TimelineInputMode> ModeChanging { get; } = Event<TimelineInputMode>.Create();
    public VirtualTimelineGrid(INoteSource notes, TimelinePlayer? player = null, TimelineInputMode[]? availableModes = null)
    {
        this.userCyclableModes = availableModes ?? [new PanMode() { Timeline = this }, new SeekMode() { Timeline  = this }, new SelectionMode() { Timeline = this }];
        Viewport = new TimelineViewport();
        Player = player ?? new TimelinePlayer(() => maxBeat, notes?.BeatsPerMinute ?? 60);
        Player.BeatChanged.Subscribe(this, static (me,b) => me.OnBeatChanged(b), this);
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Focused.Subscribe(EnableKeyboardInput, this);
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220))).Fill();
        Viewport.SubscribeToAnyPropertyChange(backgroundGrid, _ => UpdateAlternatingBackgroundOffset(), backgroundGrid);
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);
        LoadNotes(notes);
        AudioPlayer = new MelodyPlayer(this.notes, Player.BeatsPerMinute);
        CurrentMode = this.userCyclableModes[0];
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

    private void LoadNotes(INoteSource? notes)
    {
        this.notes = notes;
        if (notes == null)
        {
            maxBeat = 0;
            instrumentColorMap = new Dictionary<string, RGB>();
            AudioPlayer = new MelodyPlayer(new ListNoteSource(), Player.BeatsPerMinute);
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
        AudioPlayer = new MelodyPlayer(this.notes, Player.BeatsPerMinute);
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

    private void OnBeatChanged(double beat)
    {
        if (beat > Viewport.FirstVisibleBeat + Viewport.BeatsOnScreen * 0.8)
        {
            Viewport.FirstVisibleBeat = ConsoleMath.Round(beat - Viewport.BeatsOnScreen * 0.2);
            RefreshVisibleSet();
        }
        else if (beat < Viewport.FirstVisibleBeat)
        {
            Viewport.FirstVisibleBeat = Math.Max(0, ConsoleMath.Round(beat - Viewport.BeatsOnScreen * 0.8));
            RefreshVisibleSet();
        }
    }

    public void StartPlayback()
    {
        SoundProvider.Current.NotePlaying.SubscribeOnce(this, static (me, note) =>
        {
            me.Player.Start(note.StartBeat);
            me.PlaybackStarting.Fire(me.CurrentBeat);
        });
        AudioPlayer?.PlayFrom(CurrentBeat);
    }

    public void StopPlayback() => Player.Stop();

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);

        CurrentMode?.Paint(context);

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
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Key == ConsoleKey.Spacebar)
            {
                if (Player.IsPlaying) Player.Pause();
                else StartPlayback();
            }
            else if(k.Key == ConsoleKey.A && k.Modifiers == ConsoleModifiers.Control)
            {
                SelectedNotes.Clear();
                SelectedNotes.AddRange(NoteSource);
                RefreshVisibleSet();
            }
            else if (k.Key == ConsoleKey.D && k.Modifiers == ConsoleModifiers.Control)
            {
                SelectedNotes.Clear();
                RefreshVisibleSet();
            }
            else if(k.Key == ConsoleKey.Delete && NoteSource is ListNoteSource listSource)
            {
                foreach(var note in SelectedNotes)
                {
                    listSource.Remove(note);
                }
                SelectedNotes.Clear();
                RefreshVisibleSet();
            }
            else if(k.Key == ConsoleKey.P && k.Modifiers == 0 && pendingAddNote != null)
            {
                CommitAddNote();
            }
            else if(k.Key == ConsoleKey.D && k.Modifiers.HasFlag(ConsoleModifiers.Alt) && pendingAddNote != null)
            {
                ClearAddNotePreview();
                RefreshVisibleSet();
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
            else if (k.Key == ConsoleKey.M) NextMode(); // For mode cycling
            else CurrentMode.HandleKeyInput(k);
        }, focusLifetime);
    }

    HashSet<NoteExpression> visibleNow = new HashSet<NoteExpression>();
    public void RefreshVisibleSet()
    {
        if(live.Count == 0 && notes.Count > 0)
        {
            Viewport.FirstVisibleMidi = Math.Max(0, notes.Where(n => n.Velocity > 0).Select(m => m.MidiNote).DefaultIfEmpty(TimelineViewport.DefaultFirstVisibleMidi).Min() - 12);
        }
        maxBeat = notes.Select(n => n.StartBeat + (n.DurationBeats >= 0 ? n.DurationBeats : GetSustainedNoteDurationBeats(n))).DefaultIfEmpty(0).Max();
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

        PositionAddNotePreview();
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
        double sustainedBeats = Player.CurrentBeat - n.StartBeat;
        return Math.Max(0, sustainedBeats);
    }
    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    private void PositionAddNotePreview()
    {
        if (pendingAddNote == null || addNotePreview == null) return;
        var (start, duration, midi) = pendingAddNote.Value;
        int x = ConsoleMath.Round((start - Viewport.FirstVisibleBeat) / BeatsPerColumn) * ColWidthChars;
        int y = (Viewport.FirstVisibleMidi + Viewport.MidisOnScreen - 1 - midi) * RowHeightChars;
        int w = Math.Max(1, ConsoleMath.Round(duration / BeatsPerColumn) * ColWidthChars);
        int h = RowHeightChars;
        addNotePreview.MoveTo(x, y);
        addNotePreview.ResizeTo(w, h);
    }

    internal void BeginAddNotePreview(double start, double duration, int midi)
    {
        ClearAddNotePreview();
        pendingAddNote = (start, duration, midi);
        addNotePreview = AddPreviewControl();
        addNotePreview.Background = RGB.DarkGreen;
        addNotePreview.ZIndex = 0;
        Viewport.SubscribeToAnyPropertyChange(addNotePreview, _ => PositionAddNotePreview(), addNotePreview);
        PositionAddNotePreview();
        StatusChanged.Fire(ConsoleString.Parse("[White]Press [Cyan]p[White] to add a note here or press ALT + D to deselect."));
    }

    internal void ClearAddNotePreview()
    {
        addNotePreview?.Dispose();
        addNotePreview = null;
        pendingAddNote = null;
    }

    internal void CommitAddNote()
    {
        if (pendingAddNote == null || NoteSource is not ListNoteSource list) return;
        var (start, duration, midi) = pendingAddNote.Value;
        list.Add(NoteExpression.Create(midi, start, duration,instrument: InstrumentExpression.Create("Keyboard", InstrumentFactory)));
        ClearAddNotePreview();
        RefreshVisibleSet();
    }
}
