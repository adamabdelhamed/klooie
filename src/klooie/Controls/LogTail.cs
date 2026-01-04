using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class LogTail : ProtectedConsolePanel
{
    private StackPanel logRendererStack;
    private static LazyPool<LogTail> pool = new LazyPool<LogTail>(() => new LogTail());

    public float MaxHeightFractionOfParent { get; set; } = 0.5f;

    private LogTail()
    {
        this.CompositionMode = CompositionMode.BlendBackground;
        ProtectedPanel.CompositionMode = CompositionMode.BlendBackground;
    }

    public static LogTail Create()
    {
        var ret = pool.Value.Rent();
        ret.logRendererStack = StackPanelPool.Instance.Rent();
        ret.logRendererStack.AutoSize = StackPanel.AutoSizeMode.Both;
        ret.ProtectedPanel.Add(ret.logRendererStack).Fill();
        ret.logRendererStack.BoundsChanged.Sync(ret, static me => me.SyncBounds(), ret);
        return ret;
    }

    public void WriteLine(string s) => WriteLine(s.ToWhite());
    public void WriteLine(ConsoleString s)
    {
        var lineRenderer = ConsoleStringRendererPool.Instance.Rent();
        lineRenderer.CanFocus = false;
        lineRenderer.Content = s;
        lineRenderer.CompositionMode = CompositionMode.BlendBackground;
        logRendererStack.Controls.Add(lineRenderer);

        while (Height > Parent.Height * MaxHeightFractionOfParent)
        {
            logRendererStack.Controls.RemoveAt(0);
        }
    }

    private void SyncBounds()
    {
        this.Width = logRendererStack.Width;
        this.Height = logRendererStack.Height;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
    }
}