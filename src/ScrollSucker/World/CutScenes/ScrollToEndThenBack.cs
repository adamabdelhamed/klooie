namespace ScrollSucker;
public class ScrollToEndThenBack : CutScene
{
    private Camera Camera => Game.Current.GamePanel as Camera;

    public ScrollToEndThenBack()
    {
        StartLocation = -420;
    }

    public override async Task Execute()
    {
        foreach(var c in Game.Current.GamePanel.Children.WhereAs<Character>().Where(c => c is Player == false).ToArray())
        {
            c.Dispose();
        }

        var destLeft = Camera.BigBounds.Right - Camera.CameraBounds.Width;
        var destTop = Camera.BigBounds.Top;
        await Camera.AnimateTo(new LocF(destLeft, destTop),2000, ease: EasingFunctions.EaseInOut);

        await Game.Current.Delay(2000);

        var player = Game.Current.GamePanel.Children.WhereAs<Player>().FirstOrDefault();
        if (player == null) return;

        await Camera.PointAnimateTo(CustomCameraMovement.GetPointerForPlayer(player), 2000, ease: EasingFunctions.EaseInOut);
    }
}
