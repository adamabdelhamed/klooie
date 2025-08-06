using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class RenameTrackCommand : ICommand
{
    private SongComposer composer;
    private ComposerTrack selectedTrack;
    private string oldTrackName;
    private string newTrackName;

    public RenameTrackCommand(SongComposer composer, ComposerTrack selectedTrack, string newTrackName)
    {
        this.composer = composer;
        this.selectedTrack = selectedTrack;
        this.oldTrackName = selectedTrack.Name;
        this.newTrackName = newTrackName;
    }

    public string Description => "Rename Track";

    public void Do()
    {
        selectedTrack.Name = newTrackName;
        composer.Grid.Session.Workspace.UpdateSong(composer.Grid.Session.CurrentSong);
    }

    public void Undo()
    {
        selectedTrack.Name = oldTrackName;
        composer.Grid.Session.Workspace.UpdateSong(composer.Grid.Session.CurrentSong);
    }
}
