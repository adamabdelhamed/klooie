using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public class MelodyComposer : Composer<NoteExpression>
{
    public Event<ConsoleString> StatusChanged { get; } = Event<ConsoleString>.Create();

    private MelodyComposerViewport vp = new MelodyComposerViewport();
    public override MelodyComposerViewport Viewport => vp;

    private Recyclable? focusLifetime;
    public MelodyComposerPlayer TimelinePlayer { get; }


    public MelodyComposerEditor Editor { get; }
    
    public InstrumentExpression? Instrument { get; set; } = new InstrumentExpression() { Name = "Default", PatchFunc = SynthLead.Create };



    public double CurrentBeat => TimelinePlayer.CurrentBeat;

    private Dictionary<string, RGB> instrumentColorMap = new();
    private readonly MelodyComposerInputMode[] userCyclableModes;

    public WorkspaceSession Session { get; private init; }

    public MelodyComposerInputMode CurrentMode { get; private set; }
    public Event<MelodyComposerInputMode> ModeChanging { get; } = Event<MelodyComposerInputMode>.Create();

    public MelodyComposer(WorkspaceSession session, ListNoteSource notes) : base(notes)
    {
        this.Session = session;
        notes = notes ?? new ListNoteSource();
        this.userCyclableModes =  [new MelodyComposerNavigationMode() { Composer = this }, new MelodyComposerSelectionMode() { Composer = this }];
        TimelinePlayer = new MelodyComposerPlayer(this);

        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);

        Focused.Subscribe(EnableKeyboardInput, this);

 
        ConsoleApp.Current.InvokeNextCycle(RefreshVisibleSet);
        InitInstrumentColors(notes);
        TimelinePlayer.BeatChanged.Subscribe(this, static (me, b) => me.RefreshVisibleSet(), this);  
        CurrentMode = this.userCyclableModes[0];
        Editor = new MelodyComposerEditor(session.Commands) { Composer = this };
        TimelinePlayer.Stopped.Subscribe(this, static (me) => me.StatusChanged.Fire(ConsoleString.Parse("[White]Stopped.")), this);
        RefreshVisibleSet();
    }

    public void SetMode(MelodyComposerInputMode mode)
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

  


    public void StopPlayback() => TimelinePlayer.Stop();

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
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
                    TimelinePlayer.Pause();
                }
                else
                {
                    TimelinePlayer.Play();
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

    private double GetSustainedNoteDurationBeats(NoteExpression n)
    {
        // Show duration from note's start to current playhead position
        double sustainedBeats = TimelinePlayer.CurrentBeat - n.StartBeat;
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

    private void InitInstrumentColors(ListNoteSource notes)
    {

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
    protected override RGB GetColor(NoteExpression note) => note.Instrument == null ? RGB.Orange : instrumentColorMap.TryGetValue(note.Instrument.Name, out var color) ? color : RGB.Orange;
}
