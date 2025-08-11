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

    private async Task InitializeAsync()
    {
        await Session.Initialize();
        ComposerWithTracks = ProtectedPanel.Add(new SongComposer(Session, midiProvider)).Fill();
        ComposerWithTracks.Grid.Focus();
    }
}
