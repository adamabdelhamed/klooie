using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class Viewport
{
    public int ColWidthChars { get; set; } = 1;
    public int RowHeightChars { get; set; } = 1;

    public double FirstVisibleBeat { get; private set; }
    public double BeatsOnScreen { get; private set; }
    public double LastVisibleBeat => FirstVisibleBeat + BeatsOnScreen;

    public int FirstVisibleRow { get; private set; }
    public int LastVisibleRow => FirstVisibleRow + RowsOnScreen - 1;
    public int RowsOnScreen { get; private set; }
    public Event Changed { get; private set; } = Event.Create();

    public void SetBeatsOnScreen(double beats)
    {
        BeatsOnScreen = Math.Max(1, beats);
        Changed.Fire();
    }

    public void SetFirstVisibleBeat(double beat)
    {
        FirstVisibleBeat = Math.Max(0, beat);
        Changed.Fire();
    }

    public void ScrollBeats(double dx)
    {
        FirstVisibleBeat = Math.Max(0, FirstVisibleBeat + dx);
        Changed.Fire();
    }

    public void OnBeatChanged(double beat)
    {
        if (beat > FirstVisibleBeat + BeatsOnScreen * 0.8)
        {
            FirstVisibleBeat = ConsoleMath.Round(beat - BeatsOnScreen * 0.2);
            Changed.Fire();
        }
        else if (beat < FirstVisibleBeat)
        {
            FirstVisibleBeat = Math.Max(0, ConsoleMath.Round(beat - BeatsOnScreen * 0.8));
            Changed.Fire();
        }
    }

    public void ScrollRows(int delta, int rowCount)
    {
        if (delta == 0) return;
        FirstVisibleRow = Math.Clamp(FirstVisibleRow + delta, 0, Math.Max(0, rowCount - RowsOnScreen));
        Changed.Fire();
    }

    public void SetFirstVisibleRow(int row)
    {
        FirstVisibleRow = Math.Max(0, row);
        Changed.Fire();
    }

    public void SetRowsOnScreen(int count)
    {
        RowsOnScreen = Math.Max(1, count);
        Changed.Fire();
    }
}
