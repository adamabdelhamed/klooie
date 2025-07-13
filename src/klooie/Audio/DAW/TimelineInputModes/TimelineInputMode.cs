using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

public abstract class TimelineInputMode : IComparable<TimelineInputMode>
{
    public required VirtualTimelineGrid Timeline { get; init; }

    public abstract void HandleKeyInput(ConsoleKeyInfo key);
    public virtual void Enter() { }

    public virtual void Paint(ConsoleBitmap context) { }

    public int CompareTo(TimelineInputMode? other)
    {
        return this.GetType().FullName == other?.GetType().FullName ? 0 : -1;
    }
}