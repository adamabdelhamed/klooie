using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class StatusBar : ProtectedConsolePanel
{
    public const int Height = 1;

    private ConsoleStringRenderer label;

    public ConsoleString Message
    {
        get => label.Content;
        set => label.Content = value;
    }

    public StatusBar()
    {
        Background = new RGB(50, 50, 50);
        label = ProtectedPanel.Add(new ConsoleStringRenderer("Ready".ToWhite()) { CompositionMode = CompositionMode.BlendBackground }).DockToLeft();
    }
}