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
        grid.RefreshVisibleCells();
        grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
    }

    public void Undo()
    {
        grid.Tracks[trackIndex].Melodies.Add(clip);
        grid.RefreshVisibleCells();
        grid.Session.Workspace.UpdateSong(grid.Session.CurrentSong);
    }
}
