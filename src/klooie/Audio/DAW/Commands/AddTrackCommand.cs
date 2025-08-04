using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AddTrackCommand : ComposerCommand
{
    private readonly ComposerWithTracks composer;
    private readonly string name;

    public AddTrackCommand(ComposerWithTracks composer, string name) : base(composer, "Add Track")
    {
        this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
        this.name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override void Do() => composer.Composer.AddTrack(name);
    public override void Undo()
    {
        var index = composer.Composer.Tracks.FindIndex(t => t.Name == name);
        if (index != -1)
        {
            composer.Composer.RemoveTrack(index);
        }
    }
}

public class DeleteTrackCommand : ComposerCommand
{
    private readonly ComposerWithTracks composer;
    private ComposerTrack deleted;
    private int index;
    public DeleteTrackCommand(ComposerWithTracks composer, ComposerTrack deleted) : base(composer, "Add Track")
    {
        this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
        this.deleted = deleted ?? throw new ArgumentNullException(nameof(deleted));
    }

    public override void Do()
    {
        index = composer.Composer.Tracks.IndexOf(deleted);
        if (index != -1)
        {
            deleted = composer.Composer.Tracks[index];
            composer.Composer.RemoveTrack(index);
        }
    }
    public override void Undo()
    {
        if (deleted != null && index >= 0)
        {
            composer.Composer.InsertTrack(index, deleted);
        }
    }
}
