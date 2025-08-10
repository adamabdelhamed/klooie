using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class UpdateBPMCommand : ComposerCommand
{
    private readonly double oldBPM;
    private readonly double newBPM;
    private readonly Dropdown ux;

    public static bool IsExecuting { get; private set; } = false;
    public UpdateBPMCommand(SongComposer composer, Dropdown ux, double newBPM) : base(composer, "Update BPM")
    {
        this.ux = ux ?? throw new ArgumentNullException(nameof(ux));
        oldBPM = WorkspaceSession.Current.CurrentSong.BeatsPerMinute;
        this.newBPM = newBPM;
    }

    public override void Do()
    {
        if(IsExecuting) return; // Prevent re-entrancy issues
        IsExecuting = true;
        SetBPM(newBPM);
        
        ux.Value = ux.Options.Where(o => (double)o.Value == newBPM).SingleOrDefault();
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        IsExecuting = false;
    }
    public override void Undo()
    {
        if(IsExecuting) return; // Prevent re-entrancy issues
        IsExecuting = true;
        SetBPM(oldBPM);
        ux.Value = ux.Options.Where(o => (double)o.Value == oldBPM).SingleOrDefault();
        WorkspaceSession.Current.Workspace.UpdateSong(WorkspaceSession.Current.CurrentSong);
        IsExecuting = false;
    }

    private void SetBPM(double bpm)
    {
        var s = WorkspaceSession.Current.CurrentSong;
        s.BeatsPerMinute = bpm;
        for (var i = 0; i < s.Tracks.Count; i++)
        {
            var track = s.Tracks[i];
            for (var j = 0; j < track.Melodies.Count; j++)
            {
                var melody = track.Melodies[j];
                for (var k = 0; k < melody.Melody.Count; k++)
                {
                    var note = s.Tracks[i].Melodies[j].Melody[k];
                    note.BeatsPerMinute = s.BeatsPerMinute;
                }
            }
        }
    }
}

 
