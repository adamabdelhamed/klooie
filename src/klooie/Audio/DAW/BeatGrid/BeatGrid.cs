using System;
using System.Collections;
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

    private readonly Dictionary<T, BeatCell<T>> visibleCells = new();
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

                var halfWidthBeats = Viewport.BeatsOnScreen * 0.5;
                var leftBeatIfCentered = Player.CurrentBeat - halfWidthBeats;
                var newFirst = Math.Max(0, leftBeatIfCentered);
                newFirst = Math.Round(newFirst / beatsPerColumn) * beatsPerColumn;
                Viewport.SetFirstVisibleBeat(newFirst);
            }
        }
    }

    public virtual bool IsNavigating => true;

    private readonly BeatGridInputMode<T>[] userCyclableModes;
    public BeatGridInputMode<T> CurrentMode { get; private set; }
    public Event<BeatGridInputMode<T>> ModeChanging { get; } = Event<BeatGridInputMode<T>>.Create();

    public BeatGrid(WorkspaceSession session)
    {
        this.Session = session ?? throw new ArgumentNullException(nameof(session));
        Viewport = new Viewport();
        userCyclableModes = GetAvailableModes();
        SetMode(userCyclableModes[0]);
        CanFocus = true;
        ProtectedPanel.Background = new RGB(240, 240, 240);

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

    protected abstract BeatCell<T> BeatCellFactory(T value);

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

            if (!visibleCells.TryGetValue(value, out BeatCell<T> cell))
            {
                cell = ProtectedPanel.Add(BeatCellFactory(value));
                cell.ZIndex = 1;
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
            {
                BeatsPerColumn /= 2; // zoom in
            }
        }
        else if (k.Key == ConsoleKey.OemMinus || k.Key == ConsoleKey.Subtract)
        {
            if (BeatsPerColumn * 2 <= MaxBeatsPerColumn)
            {
                BeatsPerColumn *= 2; // zoom out
            }
        }
        else HandleKeyInput(k);
    }

    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    public abstract Song Compose();
    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    protected abstract CellPositionInfo GetCellPositionInfo(T value);
    protected abstract double CalculateMaxBeat();
    private void PositionCell(BeatCell<T> cell)
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
