using System;
using System.Collections.Generic;
using System.Text;

namespace klooie;

public class VisualTreeStack
{
    private readonly Stack<ConsoleControl> values = new Stack<ConsoleControl>();

    public void Push(ConsoleControl c) => values.Push(c);
    public void Pop() => values.Pop();
    public void Clear() => values.Clear();

    public string DescribeCurrentPath()
    {
        if (values.Count == 0) return "Paint Stack is Empty";
        var ret = new StringBuilder();

        foreach(var frame in values.Reverse())
        {
            DescribeCurrentFrame(frame, ret);
            ret.AppendLine();
        }
        return ret.ToString();
    }

    private string DescribeCurrentFrame(ConsoleControl c, StringBuilder ret)
    {
        ret.AppendLine($"  @{c?.GetType().FullName ?? "null"}");
        if (c == null) return ret.ToString();

        ret.AppendLine($"      [Pool: {c.RecyclableState}]");
        ret.AppendLine($"      [Parent: {c.Parent?.GetType().Name ?? "null"}]");
        if (c is Container container) ret.AppendLine($"      [Children: { container?.Children?.Count.ToString() ?? "null"}]");
        if (c is ConsolePanel panel)  ret.AppendLine($"      [Controls: { panel?.Controls?.Count.ToString() ?? "null"}]");
        ret.AppendLine();
        return ret.ToString();
    }
}