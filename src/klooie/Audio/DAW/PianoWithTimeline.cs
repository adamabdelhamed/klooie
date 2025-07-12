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
    public TimelinePlayer Player => Timeline.Player;
    public PianoWithTimeline(INoteSource notes, TimelinePlayer? player = null, ConsoleControl? commandBar = null)
    {
        var rowSpecPrefix = commandBar == null ? "1r" : "1p;1r";
        var rowOffset = commandBar == null ? 0 : 1;
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Timeline = layout.Add(new VirtualTimelineGrid(notes, player), 1, rowOffset); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Timeline.Viewport), 0, rowOffset);
        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset+1, columnSpan: 2);
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