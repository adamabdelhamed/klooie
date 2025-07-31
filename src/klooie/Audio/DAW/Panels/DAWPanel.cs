using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DAWPanel : ProtectedConsolePanel
{
    public WorkspaceSession Session { get; private set; }
    public PianoWithTimeline PianoWithTimeline { get; private set; }

    public DAWPanel(WorkspaceSession session)
    {
        Session = session;
        var lastOpenedSong = session.Workspace.Settings.LastOpenedSong != null ? session.Workspace.Songs.FirstOrDefault(s => s.Title == session.Workspace.Settings.LastOpenedSong) : new SongInfo() { Title = $"Song {DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}", BeatsPerMinute = 60, Notes = new List<NoteExpression>() };
        var commandBar = new ConsoleControl() { Background = RGB.Green };
        PianoWithTimeline = ProtectedPanel.Add(new PianoWithTimeline(session, new ListNoteSource(lastOpenedSong.Notes), commandBar)).Fill();
        Ready.SubscribeOnce(PianoWithTimeline.Timeline.Focus);
    }
}
