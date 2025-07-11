using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class PianoWithTimeline : ProtectedConsolePanel
{
    private GridLayout layout;
    public PianoPanel Piano { get; private init; }
    public VirtualTimelineGrid Timeline { get; private init; }
    public StatusBar StatusBar { get; private init; }
    public PianoWithTimeline(Song song)
    {
        layout = ProtectedPanel.Add(new GridLayout($"1r;{StatusBar.Height}p", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Timeline = layout.Add(new VirtualTimelineGrid(song), 1, 0); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Timeline.Viewport), 0, 0);
        StatusBar = layout.Add(new StatusBar(), column: 0, row: 1, columnSpan: 2);
    }

    public void StartPlayback()
    {
        Timeline.StartPlayback();
        StatusBar.Message = "Playing...".ToWhite();
    }

    public void StopPlayback()
    {
        Timeline.StopPlayback();
        StatusBar.Message = "Stopped".ToWhite();
    }
}