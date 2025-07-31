using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public interface ICommand
{
    void Do();
    void Undo();
    string Description { get; }
}
