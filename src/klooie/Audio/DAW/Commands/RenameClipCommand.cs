using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class RenameClipCommand : ICommand
{
    private MelodyClip clip;
    private string oldClipName;
    private string newClipName;

    public RenameClipCommand(MelodyClip clip, string newClipName)
    {
        this.clip = clip;
        this.oldClipName = clip.Name;
        this.newClipName = newClipName;
    }

    public string Description => "Rename Clip";

    public void Do()
    {
        clip.Name = newClipName;
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
    }

    public void Undo()
    {
        clip.Name = oldClipName;
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
    }
}
