using klooie;
using System;

public class DeleteMelodyClipCommand : ICommand
{
    private readonly TrackGrid grid;
    private readonly int trackIndex;
    private readonly MelodyClip clip;

    public string Description { get; }

    public DeleteMelodyClipCommand(TrackGrid grid, int trackIndex, MelodyClip clip)
    {
        this.grid = grid;
        this.trackIndex = trackIndex;
        this.clip = clip;
        this.Description = "Delete Melody Clip";
    }

    public void Do()
    {
        grid.Tracks[trackIndex].Melodies.Remove(clip);
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        grid.RefreshVisibleCells();
    }

    public void Undo()
    {
        grid.Tracks[trackIndex].Melodies.Add(clip);
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        grid.RefreshVisibleCells();
    }
}
