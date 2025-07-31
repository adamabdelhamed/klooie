using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public partial class TimelineViewport : IObservableObject
{
    public const int DefaultFirstVisibleMidi = 50;
    public partial int FirstVisibleMidi { get; set; }
    public partial int MidisOnScreen { get; set; }
    public partial double FirstVisibleBeat { get; set; }
    public double LastVisibleBeat => FirstVisibleBeat + BeatsOnScreen;

    public partial double BeatsOnScreen { get; set; }
    public void ScrollRows(int delta) => FirstVisibleMidi = Math.Clamp(FirstVisibleMidi + delta, 0, 127);
    public void ScrollBeats(double dx) => FirstVisibleBeat = Math.Max(0, FirstVisibleBeat + dx);

    public VirtualTimelineGrid Timeline { get; }

    public TimelineViewport(VirtualTimelineGrid timeline)
    {
        this.Timeline = timeline;
        // middle c
        FirstVisibleMidi = DefaultFirstVisibleMidi;
    }

    public void OnBeatChanged(double beat)
    {
        if (beat > FirstVisibleBeat + BeatsOnScreen * 0.8)
        {
            FirstVisibleBeat = ConsoleMath.Round(beat - BeatsOnScreen * 0.2);
            Timeline.RefreshVisibleSet();
        }
        else if (beat < FirstVisibleBeat)
        {
            FirstVisibleBeat = Math.Max(0, ConsoleMath.Round(beat - BeatsOnScreen * 0.8));
            Timeline.RefreshVisibleSet();
        }
    }
}
