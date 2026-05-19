using PowerArgs;

namespace klooie.blazorSampleApp;

public class DemoApp : ConsoleApp
{
    protected override async Task Startup()
    {
        var staticLabel = ConsoleStringRendererPool.Instance.Rent();
        staticLabel.Content = "0 FPS".ToGreen();

        BeforePaint.Subscribe(() => staticLabel.Content = $"{FramesPerSecond} FPS".ToGreen(), this);

        _ = LayoutRoot.Add(staticLabel).DockToRight().DockToTop(padding: 1);

        for (var i = 0; i < 15; i++)
        {
            var label = ConsoleStringRendererPool.Instance.Rent();
            label.Content = "Animated label".ToRed(bg: RGB.Red.ToOther(RGB.Black, .9f));
            _ = LayoutRoot.Add(label);
            label.X = 2;
            label.Y = 4 + i * 2;
            await Task.Delay(1000);
            _ = label.AnimateSync(() => new RectF(LayoutRoot.Width - (label.Width + 2), label.Y, label.Width, label.Height), 3000, easingFunction: EasingFunctions.EaseInOutCinematic, autoReverse: true, loop: this);
        }
    }
}
