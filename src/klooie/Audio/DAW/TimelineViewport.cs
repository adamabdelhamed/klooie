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
    public partial double BeatsOnScreen { get; set; }
    public void ScrollRows(int delta) => FirstVisibleMidi = Math.Clamp(FirstVisibleMidi + delta, 0, 127);
    public void ScrollBeats(double dx) => FirstVisibleBeat = Math.Max(0, FirstVisibleBeat + dx);

    public TimelineViewport()
    {
        // middle c
        FirstVisibleMidi = DefaultFirstVisibleMidi;
    }
}
