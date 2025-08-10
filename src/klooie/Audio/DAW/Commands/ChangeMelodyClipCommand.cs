using klooie;
using System;

public class ChangeMelodyClipCommand : ICommand
{
    private readonly TrackGrid grid;
    private readonly int trackIndex;
    private readonly MelodyClip oldClip;
    private readonly MelodyClip newClip;

    public string Description { get; }

    public ChangeMelodyClipCommand(TrackGrid grid, int trackIndex, MelodyClip oldClip, MelodyClip newClip)
    {
        this.grid = grid;
        this.trackIndex = trackIndex;
        this.oldClip = oldClip;
        this.newClip = newClip;
        this.Description = "Change Melody Clip";
    }

    public void Do()
    {
        var melodies = grid.Tracks[trackIndex].Clips;
        int idx = melodies.IndexOf(oldClip);
        if (idx >= 0)
        {
            melodies[idx] = newClip;
            WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
            grid.RefreshVisibleCells();
            grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
        }
    }

    public void Undo()
    {
        var melodies = grid.Tracks[trackIndex].Clips;
        int idx = melodies.IndexOf(newClip);
        if (idx >= 0)
        {
            melodies[idx] = oldClip;
            WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
            grid.RefreshVisibleCells();
            grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
        }
    }
}
