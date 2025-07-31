using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MultiCommand : ICommand
{
    private readonly List<ICommand> commands;
    public MultiCommand(IEnumerable<ICommand> cmds, string description = "Multi Edit")
    {
        commands = cmds.ToList();
        Description = description;
    }
    public void Do() { foreach (var c in commands) c.Do(); }
    public void Undo() { foreach (var c in Enumerable.Reverse(commands)) c.Undo(); }
    public string Description { get; }
}