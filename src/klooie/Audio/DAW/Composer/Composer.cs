using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public abstract class Composer<T> : ProtectedConsolePanel
{
    public const int ColWidthChars = 1;
    public const int RowHeightChars = 1;

    public const double MaxBeatsPerColumn = 1.0;     // each cell is 1 beat (max zoomed out)
    public const double MinBeatsPerColumn = 1.0 / 128; // each cell is 1/8 beat (max zoomed in)

    private double beatsPerColumn  = 1 / 8.0;

    private AlternatingBackgroundGrid backgroundGrid;

    public static readonly RGB SelectedCellColor = RGB.Cyan;

    private readonly Dictionary<T, ComposerCell<T>> visibleCells = new();
    private readonly HashSet<T> tempHashSet = new HashSet<T>();

    public List<T> Values { get; private set; }
    public List<T> SelectedValues { get; } = new List<T>();
    public double MaxBeat { get; private set; }
    public abstract Viewport Viewport { get; }

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
                // TODO: if (CurrentMode is MelodyComposerSelectionMode sm) sm.SyncCursorToCurrentZoom();
            }
        }
    }

    public Composer(List<T> values)
    {
        Values = values;
        backgroundGrid = ProtectedPanel.Add(new AlternatingBackgroundGrid(0, RowHeightChars, new RGB(240, 240, 240), new RGB(220, 220, 220), RGB.Cyan.ToOther(RGB.Gray.Brighter, .95f), () => HasFocus)).Fill();
        Viewport.SetFirstVisibleRow(Math.Max(0, Values.Where(n => GetCellPositionInfo(n).IsHidden == false).Select(m => GetCellPositionInfo(m).Row).DefaultIfEmpty(0).Min()));
        BoundsChanged.Sync(UpdateViewportBounds, this);
        Viewport.Changed.Subscribe(backgroundGrid, _ =>
        {
            UpdateAlternatingBackgroundOffset();
            RefreshVisibleSet();
        }, backgroundGrid);

    }

    private void UpdateAlternatingBackgroundOffset() => backgroundGrid.CurrentOffset = ConsoleMath.Round(Viewport.FirstVisibleRow / (double)RowHeightChars);

    private void UpdateViewportBounds()
    {
        Viewport.SetBeatsOnScreen(Math.Max(1, Width * BeatsPerColumn / Viewport.ColWidthChars));
        Viewport.SetRowsOnScreen(Math.Max(1, Height / Viewport.RowHeightChars));
    }

    public void RefreshVisibleSet()
    {
        if (visibleCells.Count == 0 && Values.Count > 0)
        {
            Viewport.SetFirstVisibleRow(Math.Max(0, Values.Where(n => GetCellPositionInfo(n).IsHidden == false).Select(m => GetCellPositionInfo(m).Row).DefaultIfEmpty(0).Min()));
        }
        MaxBeat = CalculateMaxBeat();
        tempHashSet.Clear();

        for (int i = 0; i < Values.Count; i++)
        {
            var value = Values[i];
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

        //TODO: Editor.PositionAddNotePreview();
    }

    internal ConsoleControl AddPreviewControl() => ProtectedPanel.Add(new ConsoleControl());

    protected abstract CellPositionInfo GetCellPositionInfo(T value);
    protected abstract double CalculateMaxBeat();
    private void PositionCell(ComposerCell<T> cell)
    {
        var positionInfo = GetCellPositionInfo(cell.Value);
        double beatsFromLeft = positionInfo.BeatStart - Viewport.FirstVisibleBeat;

        int x = ConsoleMath.Round((positionInfo.BeatStart - Viewport.FirstVisibleBeat) / BeatsPerColumn) * Viewport.ColWidthChars;
        int y = (Viewport.FirstVisibleRow + Viewport.RowsOnScreen - 1 - positionInfo.Row) * Viewport.RowHeightChars;

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

    protected override void OnPaint(ConsoleBitmap ctx) => ctx.FillRect(Background, 0, 0, Width, Height);
}