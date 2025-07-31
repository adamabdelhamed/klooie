using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class WorkspaceSession
{
    public required Workspace Workspace { get; init; }
    public CommandStack Commands { get; } = new();
    
}