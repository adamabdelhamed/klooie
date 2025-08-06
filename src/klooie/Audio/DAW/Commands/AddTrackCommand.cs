using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class AddTrackCommand : ComposerCommand
{
    private readonly SongComposer composer;
    private readonly string name;

    public AddTrackCommand(SongComposer composer, string name) : base(composer, "Add Track")
    {
        this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
        this.name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override void Do() => composer.Grid.AddTrack(name);
    public override void Undo()
    {
        var index = composer.Grid.Tracks.FindIndex(t => t.Name == name);
        if (index != -1)
        {
            composer.Grid.RemoveTrack(index);
        }
    }
}

public class DeleteTrackCommand : ComposerCommand
{
    private readonly SongComposer composer;
    private ComposerTrack deleted;
    private int index;
    public DeleteTrackCommand(SongComposer composer, ComposerTrack deleted) : base(composer, "Add Track")
    {
        this.composer = composer ?? throw new ArgumentNullException(nameof(composer));
        this.deleted = deleted ?? throw new ArgumentNullException(nameof(deleted));
    }

    public override void Do()
    {
        index = composer.Grid.Tracks.IndexOf(deleted);
        if (index != -1)
        {
            deleted = composer.Grid.Tracks[index];
            composer.Grid.RemoveTrack(index);
        }
    }
    public override void Undo()
    {
        if (deleted != null && index >= 0)
        {
            composer.Grid.InsertTrack(index, deleted);
        }
    }
}
