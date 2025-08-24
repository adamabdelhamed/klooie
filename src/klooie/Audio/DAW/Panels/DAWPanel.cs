using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DAWPanel : ProtectedConsolePanel
{
    public WorkspaceSession Session { get; private set; }
    public SongComposer ComposerWithTracks { get; private set; }
     
    private IMidiProvider midiProvider;
    public DAWPanel(WorkspaceSession session, IMidiProvider midiProvider)
    {
        Session = session;
        this.midiProvider = midiProvider ?? throw new ArgumentNullException(nameof(midiProvider));
        Ready.SubscribeOnce(async () => await InitializeAsync());

        ConsoleApp.Current.GlobalKeyPressed.Subscribe(OnGlobalKeyPressed, this);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Escape, async () =>
        {
            var choice = await ChoiceDialog.Show(new ShowMessageOptions("Are you sure you want to exit?") { UserChoices = DialogChoice.YesNo, SpeedPercentage = 0, DialogWidth = 50, });
            if ("yes".Equals(choice?.Value?.ToString(), StringComparison.OrdinalIgnoreCase)) ConsoleApp.Current.Stop();
        }, this);
    }

    private void OnGlobalKeyPressed(ConsoleKeyInfo info)
    {
        if(info.Key == ConsoleKey.Z && info.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            Session.Commands.Undo();
        }
        else if(info.Key == ConsoleKey.Y && info.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            Session.Commands.Redo();
        }
    }

    private void AddNewSongCommand()
    {
        var uiHint = new ConsoleStringRenderer(ConsoleString.Parse("[B=Cyan][Black] ALT + N [D][White] New Song"));
        ComposerWithTracks.CommandBar.Controls.Insert(0, uiHint);
        ConsoleApp.Current.GlobalKeyPressed.Subscribe(async k =>
        {
            if (k.Modifiers != ConsoleModifiers.Alt || k.Key != ConsoleKey.N) return;

            try
            {
                await WorkspaceSession.Current.NewSong();
                ComposerWithTracks.Dispose();
                ComposerWithTracks = ProtectedPanel.Add(new SongComposer(Session, midiProvider)).Fill();
                ComposerWithTracks.Grid.Focus();
                AddNewSongCommand();
                AddOpenSongCommand();
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine(ex.Message.ToRed());
            }
        }, uiHint);
    }

    private void AddOpenSongCommand()
    {
        var uiHint = new ConsoleStringRenderer(ConsoleString.Parse("[B=Cyan][Black] ALT + O [D][White] Open Song"));
        ComposerWithTracks.CommandBar.Controls.Insert(1, uiHint);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.O, ConsoleModifiers.Alt, async () =>
        {
            try
            {
                var opened = await WorkspaceSession.Current.OpenSong();
                if(opened == false) return;
                ComposerWithTracks.Dispose();
                ComposerWithTracks = ProtectedPanel.Add(new SongComposer(Session, midiProvider)).Fill();
                ComposerWithTracks.Grid.Focus();
                AddNewSongCommand();
                AddOpenSongCommand();
            }
            catch (Exception ex)
            {
                ConsoleApp.Current.WriteLine(ex.Message.ToRed());
            }
        }, uiHint);
    }

    private async Task InitializeAsync()
    {
        await Session.Initialize();
        ComposerWithTracks = ProtectedPanel.Add(new SongComposer(Session, midiProvider)).Fill();
        AddNewSongCommand();
        AddOpenSongCommand();
        ComposerWithTracks.Grid.Focus();
    }
}
