

using klooie;
using klooie.Gaming;
using PowerArgs;

VeldridTerminalHost.Init(); // Warms up the infrastructure so that the ConsoleApp infra knows it's not in a "real" console environment and so that the terminal host is ready to go when we attach it to the root panel.
var app = new Game();
app.Invoke(() =>
{
    VeldridTerminalHost.BoardZoom = 4; // Zoomed in to show the effect more clearly; adjust as needed (1 is normal)
    VeldridTerminalHost.Attach((LayoutRootPanel)app.LayoutRoot);
    // Large 3d letters centered on the screen
    var collider = app.GamePanel.Add(new TextCollider("3D Sample".ToYellow()) { X = 4, Y = 4 });
    collider.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
    collider.Velocity.Angle = 30;
    collider.Velocity.Speed = 40;
    app.GamePanel.Add(new BorderCollider()).DockToRight().FillVertically();
    app.GamePanel.Add(new BorderCollider()).DockToLeft().FillVertically();
    app.GamePanel.Add(new BorderCollider()).DockToTop().FillHorizontally();
    app.GamePanel.Add(new BorderCollider()).DockToBottom().FillHorizontally();
});

app.Run();

public class BorderCollider : GameCollider
{
    private ConsoleCharacter pen = new ConsoleCharacter('/', RGB.Orange);
    protected override void OnPaint(ConsoleBitmap context) => context.Fill(pen);
}