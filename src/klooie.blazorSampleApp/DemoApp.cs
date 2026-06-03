using PowerArgs;

namespace klooie.blazorSampleApp;

public class DemoApp : ConsoleApp
{
    protected override async Task Startup()
    {
        var hudLabel = ConsoleStringRendererPool.Instance.Rent();
        hudLabel.Content = "HUD: READY".ToYellow(bg: RGB.DarkBlue);
        _ = LayoutRoot.Add(hudLabel);
        hudLabel.X = 1;
        hudLabel.Y = 1;
        _ = Presentation.ScaleRegion(new Rect(0, 0, 18, 3), 1.6f, new RegionScaleOptions { Anchor = ConsoleBitmapPresentationAnchor.TopLeft, OffsetX = 1, OffsetY = 1 }, this);

        var staticLabel = ConsoleStringRendererPool.Instance.Rent();
        staticLabel.Content = "0 FPS".ToGreen();

        BeforePaint.Subscribe(() => staticLabel.Content = $"{FramesPerSecond} FPS".ToGreen(), this);

        LayoutRoot.Add(staticLabel).DockToRight().DockToTop(padding: 1);

        var commandPanel = LayoutRoot.Add(new ConsolePanel
        {
            Background = new RGB(30, 30, 30),
            Height = 3,
            ZIndex = int.MaxValue - 10
        }).FillHorizontally().DockToBottom();

        var commandText = ConsoleStringRendererPool.Instance.Rent();
        commandText.Content = "Focused bottom command bar text with about sixty chars".ToCyan(bg: new RGB(30, 30, 30));
        commandPanel.Add(commandText).CenterBoth();
        _ = Presentation.FocusControl(commandText, static text => text.AbsoluteBounds.Center.ToRect(text.Width, 3).ToRect(), new FocusRegionOptions
        {
            Anchor = ConsoleBitmapPresentationAnchor.Bottom,
            Padding = .08f,
            AnimationMilliseconds = 650
        }, commandPanel);

        for (var i = 0; i < 15; i++)
        {
            var label = ConsoleStringRendererPool.Instance.Rent();
            label.Content = "Animated label".ToRed(bg: RGB.Red.ToOther(RGB.Black, .9f));
            LayoutRoot.Add(label);
            label.X = 2;
            label.Y = 4 + i * 2;
            await Task.Delay(1000);
            label.AnimateSync(() => new RectF(LayoutRoot.Width - (label.Width + 2), label.Y, label.Width, label.Height), 3000, easingFunction: EasingFunctions.EaseInOutCinematic, autoReverse: true, loop: this);
        }

    }
}
