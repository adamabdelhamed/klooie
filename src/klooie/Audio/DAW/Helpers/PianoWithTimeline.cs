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
    public MelodyComposer Timeline { get; private init; }

    public StatusBar StatusBar { get; private init; }
    public ComposerPlayer<NoteExpression> Player => Timeline.Player;
    public PianoWithTimeline(WorkspaceSession session, ListNoteSource notes, ConsoleControl? commandBar = null)
    {
        var rowSpecPrefix = commandBar == null ? "1r" : "1p;1r";
        var rowOffset = commandBar == null ? 0 : 1;
        layout = ProtectedPanel.Add(new GridLayout($"{rowSpecPrefix};{StatusBar.Height}p", $"{PianoPanel.KeyWidth}p;1r")).Fill();
        Timeline = layout.Add(new MelodyComposer(session, notes), 1, rowOffset); // col then row here - I know its strange
        Piano = layout.Add(new PianoPanel(Timeline.Viewport), 0, rowOffset);
        StatusBar = layout.Add(new StatusBar(), column: 0, row: rowOffset+1, columnSpan: 2);
        if(commandBar != null)
        {
            layout.Add(commandBar, 0, 0, columnSpan: 2);
        }
        Timeline.StatusChanged.Subscribe(message=> StatusBar.Message = message, this);
    }
}