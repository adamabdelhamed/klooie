namespace klooie.Gaming;
public class Pistol : Weapon
{
    public float Speed { get; set; } = 120;
    public ConsoleCharacter ProjectilePen { get; set; } = new ConsoleCharacter('*', RGB.Red);
    public Func<float> AngleVariation { get; set; } = () => 0;
    public Angle LastFireAngle { get; private set; }

    public override void FireInternal(bool alt)
    {
        LastFireAngle = CalculateAngleToTarget().Add(AngleVariation());
        var bullet = ProjectilePool.Instance.Rent(out int lease);
        bullet.Bind(this, Speed, LastFireAngle);
        bullet.Pen = ProjectilePen;
        if (bullet.IsStillValid(lease))
        {
            Game.Current.GamePanel.Add(bullet);
        }
    }
}