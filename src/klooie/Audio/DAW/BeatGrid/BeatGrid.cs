using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public abstract class BeatGrid<T> : ProtectedConsolePanel
{
    public const double MaxBeatsPerColumn = 1.0;     // each cell is 1 beat (max zoomed out)
    public const double MinBeatsPerColumn = 1.0 / 128; // each cell is 1/8 beat (max zoomed in)

    public Event<ConsoleString> StatusChanged { get; } = Event<ConsoleString>.Create();
    public Event Refreshed { get; } = Event.Create();

    private double beatsPerColumn  = 1 / 8.0;

    private BeatGridBackground<T> backgroundGrid;

    public static readonly RGB SelectedCellColor = RGB.Cyan;

    private readonly Dictionary<T, ComposerCell<T>> visibleCells = new();
    private readonly HashSet<T> tempHashSet = new HashSet<T>();

    public WorkspaceSession Session { get; private init; }
    public List<T> SelectedValues { get; } = new List<T>();
    public double MaxBeat { get; private set; }
    public Viewport Viewport { get; private set; }  

    public BeatGridPlayer<T> Player { get; }

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
                RefreshVisibleCells();
            }
        }
    }

    public double BeatsPerMinute { get; private set; }

    public virtual bool IsNavigating => true;

    private readonly BeatGridInputMode<T>[] userCyclableModes;
    public BeatGridInputMode<T> CurrentMode { get; private set; }
    public Event<BeatGridInputMode<T>> ModeChanging { get; } = Event<BeatGridInputMode<T>>.Create();

    public BeatGrid(WorkspaceSession session,  double bpm)
    {
        this.Session = session ?? throw new ArgumentNullException(nameof(session));
        Viewport = new Viewport();
        userCyclableModes = GetAvailableModes();
        SetMode(userCyclableModes[0]);
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);
        BeatsPerMinute = bpm;

        backgroundGrid = ProtectedPanel.Add(new BeatGridBackground<T>(0, this, new RGB(240, 240, 240), new RGB(220, 220, 220), RGB.Cyan.ToOther(RGB.Gray.Brighter, .95f), () => HasFocus)).Fill();
        
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Player = new BeatGridPlayer<T>(this);
        Viewport.Changed.Subscribe(backgroundGrid, _ =>
        {
            UpdateAlternatingBackgroundOffset();
            RefreshVisibleCells();
        }, backgroundGrid);

        Player.BeatChanged.Subscribe(this, static (me, b) => me.RefreshVisibleCells(), this);
        Player.Stopped.Subscribe(this, static (me) => me.StatusChanged.Fire(ConsoleString.Parse("[White]Stopped.")), this);
        ConsoleApp.Current.InvokeNextCycle(()=>
        {
            RefreshVisibleCells();
        });
        KeyInputReceived.Subscribe(EnableKeyboardInput, this);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        base.OnPaint(context);
        CurrentMode?.Paint(context);
    }

    public void NextMode() => SetMode(userCyclableModes[(Array.IndexOf(userCyclableModes, CurrentMode) + 1) % userCyclableModes.Length]);


    public void SetMode(BeatGridInputMode<T> mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ModeChanging.Fire(mode);
        CurrentMode.Enter();
    }

    protected abstract BeatGridInputMode<T>[] GetAvailableModes();

    private void UpdateAlternatingBackgroundOffset() => backgroundGrid.CurrentOffset = ConsoleMath.Round(Viewport.FirstVisibleRow / (double)Viewport.RowHeightChars);

    private void UpdateViewportBounds()
    {
        Viewport.SetBeatsOnScreen(Math.Max(1, Width * BeatsPerColumn / Viewport.ColWidthChars));
        Viewport.SetRowsOnScreen(Math.Max(1, Height / Viewport.RowHeightChars));
    }

    protected abstract IEnumerable<T> EnumerateValues();

    public void RefreshVisibleCells()
    {
        MaxBeat = CalculateMaxBeat();
        tempHashSet.Clear();

        foreach(var value in EnumerateValues())
        { 
            var positionInfo = GetCellPositionInfo(value);
            if (positionInfo.IsHidden) continue;

            bool isVisible =
                (positionInfo.BeatEnd >= Viewport.FirstVisibleBeat) &&
                (positionInfo.BeatStart <= Viewport.LastVisibleBeat) &&
                (positionInfo.Row >= Viewport.FirstVisibleRow) &&
                (positionInfo.Row <= Viewport.LastVisibleRow);

            if (!isVisible) continue;

            tempHashSet.Add(value);

            if (!visibleCells.TryGetValue(value, out ComposerCell<T> cell))
            {
                cell = ProtectedPanel.Add(new ComposerCell<T>(value) { ZIndex = 1 });
                visibleCells[value] = cell;
            }
            cell.Background = SelectedValues.Contains(value) ? SelectedCellColor :GetColor(value);
            PositionCell(cell);
        }

        // Remove cells that are no longer visible
        foreach (var kvp in visibleCells.ToArray())
        {
            if (!tempHashSet.Contains(kvp.Key))
            {
                kvp.Value.Dispose();
                visibleCells.Remove(kvp.Key);
            }
        }

        Refreshed.Fire();
    }

    public void EnableKeyboardInput(ConsoleKeyInfo k)
    {
   
        if (k.Key == ConsoleKey.Spacebar)
        {
            if (Player.IsPlaying)
            {
                Player.Pause();
            }
            else
            {
                Player.Play();
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
        else HandleKeyInput(k);
    }

    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    public abstract Song Compose();
    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    protected abstract CellPositionInfo GetCellPositionInfo(T value);
    protected abstract double CalculateMaxBeat();
    private void PositionCell(ComposerCell<T> cell)
    {
        var positionInfo = GetCellPositionInfo(cell.Value);
        double beatsFromLeft = positionInfo.BeatStart - Viewport.FirstVisibleBeat;

        int x = ConsoleMath.Round((positionInfo.BeatStart - Viewport.FirstVisibleBeat) / BeatsPerColumn) * Viewport.ColWidthChars;
        int y = (positionInfo.Row - Viewport.FirstVisibleRow) * Viewport.RowHeightChars;

        var duration = positionInfo.BeatEnd - positionInfo.BeatStart;
        int w = (int)Math.Max(1, ConsoleMath.Round(duration / BeatsPerColumn) * Viewport.ColWidthChars);
        int h = Viewport.RowHeightChars;

        cell.MoveTo(x, y);
        cell.ResizeTo(w, h);
    }
    protected abstract RGB GetColor(T value);
}

public struct CellPositionInfo
{
    public double BeatStart;
    public double BeatEnd;
    public int Row;
    public bool IsHidden;
}

public class ComposerCell<T> : ConsoleControl
{
    public readonly T Value;
    public ComposerCell(T value)
    {
        (Value, CanFocus) = (value, false);
        Foreground = RGB.Black;
    }

    protected override void OnPaint(ConsoleBitmap ctx)
    {
        base.OnPaint(ctx);

        if (typeof(T) == typeof(MelodyClip))
        {
            ctx.DrawRect(new ConsoleCharacter('#', Background.Darker, Background),0,0, Width, Height);
        }

    }


    private static readonly RGB[] BaseTrackColors = new[]
    {
        new RGB(220, 60, 60),
        new RGB(60, 180, 90),
        new RGB(65, 105, 225),
        new RGB(240, 200, 60),
        new RGB(200, 60, 200),
        new RGB(50, 220, 210),
        new RGB(245, 140, 30),
    };

    private static readonly float[] PaleFractions = new[]
    {
        0.0f,
        0.35f,
        0.7f,
    };

    public static RGB GetColor(int index)
    {
        int baseCount = BaseTrackColors.Length;
        int shade = index / baseCount;
        int colorIdx = index % baseCount;
        float pale = PaleFractions[Math.Min(shade, PaleFractions.Length - 1)];
        RGB color = BaseTrackColors[colorIdx];
        return color.ToOther(RGB.White, pale);
    }
}