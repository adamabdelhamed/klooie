using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class MelodyComposer : Composer<NoteExpression>
{
    public MelodyComposerEditor Editor { get; }

    public InstrumentExpression Instrument { get; set; } = new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };

    private Dictionary<string, RGB> instrumentColorMap = new();
    private readonly ComposerInputMode<NoteExpression>[] userCyclableModes;

    public ComposerInputMode<NoteExpression> CurrentMode { get; private set; }
    public Event<ComposerInputMode<NoteExpression>> ModeChanging { get; } = Event<ComposerInputMode<NoteExpression>>.Create();

    public override bool IsNavigating => CurrentMode is MelodyComposerNavigationMode;

    public MelodyComposer(WorkspaceSession session, ListNoteSource notes) : base(session, notes, notes.BeatsPerMinute)
    {
        this.userCyclableModes =  [new MelodyComposerNavigationMode() { Composer = this }, new MelodyComposerSelectionMode() { Composer = this }];
        SetMode(userCyclableModes[0]);
        InitInstrumentColors();
        Editor = new MelodyComposerEditor(session.Commands) { Composer = this };
        Viewport.Changed.Subscribe(() => (CurrentMode as MelodyComposerSelectionMode)?.SyncCursorToCurrentZoom(), this);
        Refreshed.Subscribe(Editor.PositionAddNotePreview, this);
    }

    public void SetMode(ComposerInputMode<NoteExpression> mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ModeChanging.Fire(mode);
        CurrentMode.Enter();
    }

    public void NextMode() => SetMode(userCyclableModes[(Array.IndexOf(userCyclableModes, CurrentMode) + 1) % userCyclableModes.Length]);
    
    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
    }

    public override void HandleKeyInput(ConsoleKeyInfo k)
    {
        if (k.Key == ConsoleKey.M)
        {
            NextMode();
        }
        else if (!Editor.HandleKeyInput(k))
        {
            CurrentMode.HandleKeyInput(k);
        }
    }

    private double GetSustainedNoteDurationBeats(NoteExpression n)
    {
        double sustainedBeats = Player.CurrentBeat - n.StartBeat;
        return Math.Max(0, sustainedBeats);
    }

    protected override CellPositionInfo GetCellPositionInfo(NoteExpression value) => new CellPositionInfo()
    {
        BeatStart = value.StartBeat,
        BeatEnd = value.StartBeat + (value.DurationBeats > 0 ? value.DurationBeats : GetSustainedNoteDurationBeats(value)),
        IsHidden = value.Velocity <= 0,
        Row = value.MidiNote - Viewport.FirstVisibleRow,
    };

    protected override double CalculateMaxBeat() => Values.Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty(0).Max();

    private void InitInstrumentColors()
    {
        var instruments = Values.Where(n => n.Instrument != null).Select(n => n.Instrument.Name).Distinct().ToArray();
        var instrumentColors = instruments.Select((s, i) => GetInstrumentColor(i)).ToArray();
        for (int i = 0; i < instruments.Length; i++) instrumentColorMap[instruments[i]] = instrumentColors[i];
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
    protected override RGB GetColor(NoteExpression note) => note.Instrument == null ? RGB.Orange : instrumentColorMap.TryGetValue(note.Instrument.Name, out var color) ? color : RGB.Orange;
    public override Song Compose() => new Song(Values as ListNoteSource, BeatsPerMinute);
    protected override Viewport CreateViewport() => new MelodyComposerViewport();
}
