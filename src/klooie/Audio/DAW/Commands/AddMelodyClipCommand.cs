using klooie;
using System;

public class AddMelodyClipCommand : ICommand
{
    private readonly TrackGrid grid;
    private readonly int trackIndex;
    private readonly MelodyClip clip;

    public string Description { get; }

    public AddMelodyClipCommand(TrackGrid grid, int trackIndex, MelodyClip clip)
    {
        this.grid = grid;
        this.trackIndex = trackIndex;
        this.clip = clip;
        this.Description = "Add Melody Clip";
    }

    public void Do()
    {
        grid.Tracks[trackIndex].Clips.Add(clip);
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        grid.RefreshVisibleCells();
        grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
    }

    public void Undo()
    {
        grid.Tracks[trackIndex].Clips.Remove(clip);
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        grid.RefreshVisibleCells();
        grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
    }
}
