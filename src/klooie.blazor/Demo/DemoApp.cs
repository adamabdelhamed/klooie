using PowerArgs;

namespace klooie.blazor.Demo;

public class DemoApp : ConsoleApp
{
    protected override Task Startup()
    {
        var staticLabel = ConsoleStringRendererPool.Instance.Rent();
        staticLabel.Content = "Static label".ToGreen();
        
        LayoutRoot.Add(staticLabel).DockToRight().DockToTop(padding: 1);

        var label = ConsoleStringRendererPool.Instance.Rent();
        label.Content = "Animated label".ToRed(bg: RGB.Red.ToOther(RGB.Black, .9f));
        LayoutRoot.Add(label);
        label.X = 2;
        label.Y = 4;
        var dest = new RectF( LayoutRoot.Width - (label.Width + 2), label.Y, label.Width, label.Height);
        label.AnimateSync(()=> dest, 1000, autoReverse: true, loop: this);
        return Task.CompletedTask;
    }
}
