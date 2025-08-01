using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class MultiCommand : ICommand
{
    private readonly List<ICommand> commands;

    public string Description { get; }

    public MultiCommand(IEnumerable<ICommand> cmds, string description = "Multi Edit")
    {
        commands = cmds.ToList();
        Description = description;
    }
    public void Do() 
    {
        for (int i = 0; i < commands.Count; i++)
        {
             commands[i].Do();
        }
    }
    public void Undo() 
    {
        for (int i = commands.Count - 1; i >= 0; i--)
        {
            commands[i].Undo();
        }
    }
}