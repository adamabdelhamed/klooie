using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DAWPanel : ProtectedConsolePanel
{
    public WorkspaceSession Session { get; private set; }
    public ComposerWithTracks ComposerWithTracks { get; private set; }
     
    private IMidiProvider midiProvider;
    public DAWPanel(WorkspaceSession session, IMidiProvider midiProvider)
    {
        Session = session;
        this.midiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        Ready.SubscribeOnce(async () => await InitializeAsync());
    }

    private async Task InitializeAsync()
    {
        await Session.Initialize();

        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Orientation = Orientation.Horizontal };

        ComposerWithTracks = ProtectedPanel.Add(new ComposerWithTracks(Session, Session.CurrentSong.Tracks, commandBar)).Fill();
        ComposerWithTracks.Composer.Focus();

 
        ExportSongUXHelper.SetupExport(() => ComposerWithTracks.Composer.Compose(), commandBar);
    }
}
