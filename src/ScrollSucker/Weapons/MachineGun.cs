namespace ScrollSucker;
public class MachineGun : Pistol
{
    private const float soundThrottle = 100;
    private TimeSpan lastFireTime;
    private static Random r = new Random();
    public MachineGun() : base()
    {
        ProjectilePen = "*".ToWhite();
        Speed = 120;
        AngleVariation = () => r.Next(-1, 1);
        lastFireTime = Game.Current.MainColliderGroup.Now - TimeSpan.FromSeconds(soundThrottle);
    }

    public override void FireInternal(bool alt)
    {
        if (Game.Current.MainColliderGroup.Now - lastFireTime >= TimeSpan.FromMilliseconds(soundThrottle))
        {
            Game.Current.Sound.Play("machinegun");
            Game.Current.Invoke(async () =>
            {
                await Game.Current.Delay(600);
                Game.Current.Sound.Play("shellsfall");
            });
            lastFireTime = Game.Current.MainColliderGroup.Now;
        }

        var shell = Game.Current.GamePanel.Add(new Shell());

        var initSpot = Holder.Center().RadialOffset(LastFireAngle.Opposite(), 1);
        shell.MoveTo(initSpot.Left, initSpot.Top, -100);

        Game.Current.Invoke(async () =>
        {
            var shellV = shell.Velocity;
            shellV.Angle = LastFireAngle.Opposite().Add(r.Next(-15, 15));
            shellV.Speed = 20;
            await Game.Current.Delay(200);
            shellV.Angle = Angle.Down.Add(r.Next(-15, 15));
            await Game.Current.Delay(200);
            shell.Content = shell.Content.ToYellow();
            shellV.Speed = 30;
            await Game.Current.Delay(200);
            shellV.Speed = 50;
            await Game.Current.Delay(200);
            shellV.Speed = 90;
            await Game.Current.Delay(200);
            shellV.Speed = 120;
            await Game.Current.Delay(1000);
            shell.Dispose();
        });

        base.FireInternal(alt);
    }
}

public class Shell : TextCollider
{
    public Shell() : base("o".ToRed())
    {
        CompositionMode = CompositionMode.BlendBackground;
    }

    public override bool CanCollideWith(GameCollider other)
    {
        return false;
    }
}

