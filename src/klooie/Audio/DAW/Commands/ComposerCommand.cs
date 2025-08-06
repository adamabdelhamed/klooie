using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class ComposerCommand : ICommand
{
    protected readonly SongComposer Composer;

    public string Description { get; }

    public ComposerCommand(SongComposer composer, string desc)
    {
        this.Composer = composer;
        this.Description = desc;
    }

    public virtual void Do()
    {
        Composer.Grid.RefreshVisibleCells();
    }

    public virtual void Undo()
    {
        Composer.Grid.RefreshVisibleCells();
    }
}