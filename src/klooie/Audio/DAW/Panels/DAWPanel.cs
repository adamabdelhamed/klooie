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

    private DAWMidi midi;
    private IMidiProductDiscoverer midiProvider;
    public DAWPanel(WorkspaceSession session, IMidiProductDiscoverer midiProvider)
    {
        Session = session;
        this.midiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        Ready.SubscribeOnce(async () => await InitializeAsync());
    }

    private async Task InitializeAsync()
    {
        await Session.Initialize();

        var commandBar = new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Margin = 2, Background = RGB.Green };

        PianoWithTimeline = ProtectedPanel.Add(new PianoWithTimeline(Session, Session.CurrentSong.Notes, commandBar)).Fill();
        PianoWithTimeline.Timeline.Focus();

        this.midi = DAWMidi.Create(midiProvider ?? throw new ArgumentNullException(nameof(midiProvider)), PianoWithTimeline);
        commandBar.Add(midi.CreateMidiProductDropdown());
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        midi?.Dispose();
        midi = null!;
    }
}
