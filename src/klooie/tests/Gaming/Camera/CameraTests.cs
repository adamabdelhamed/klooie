using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class CameraTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Camera_AlwaysCenteredFocalPoint() => GamingTest.Run(new GamingTestOptions()
    {
        Camera = true,
        TestId = TestContext.TestId(),
        Mode = UITestMode.KeyFramesVerified,
        Test = async (context) =>
        {
            var camera = Game.Current.GamePanel as Camera;
            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.BigBounds.Hypotenous > camera.Bounds.Hypotenous);
            Game.Current.GamePanel.Background = new RGB(20, 35, 20);
            PlaceBackgroundTexture();
            var fc = camera.Add(new TextCollider("focal point".ToGreen()));
            var cameraOperator = new CameraOperator(camera, fc, fc.Velocity, Game.Current, new AlwaysCenteredMovement());

            fc.MoveTo(Game.Current.GameBounds.Left, Game.Current.GameBounds.Top);
            await context.PaintAndRecordKeyFrameAsync();

            fc.MoveTo(Game.Current.GameBounds.Right - fc.Width, Game.Current.GameBounds.Top);
            await context.PaintAndRecordKeyFrameAsync();

            fc.MoveTo(Game.Current.GameBounds.Left, Game.Current.GameBounds.Bottom - fc.Height);
            await context.PaintAndRecordKeyFrameAsync();

            fc.MoveTo(Game.Current.GameBounds.Right - fc.Width, Game.Current.GameBounds.Bottom - fc.Height);
            await context.PaintAndRecordKeyFrameAsync();
            Game.Current.Stop();
        }
    });



    private void PlaceBackgroundTexture(float margin = 5)
    {
        for (var x = margin; x < Game.Current.GameBounds.Width - margin; x += 20)
        {
            for (var y = margin / 2f; y < Game.Current.GameBounds.Height - margin / 2f; y += 10)
            {
                Game.Current.GamePanel.Add(new NoFrillsLabel((x+","+y).ToConsoleString(RGB.White)) { CompositionMode = CompositionMode.BlendBackground }).MoveTo(Game.Current.GameBounds.Left + x, Game.Current.GameBounds.Top + y);
            }
        }
    }
}
