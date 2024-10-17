namespace ScrollSucker;
public class Pistol : Weapon
{
    public float Speed { get; set; } = 60;
    public ConsoleString ProjectilePen { get; set; } = "*".ToRed();
    public override WeaponStyle Style => WeaponStyle.Primary;
    public Func<float> AngleVariation { get; set; } = () => 0;
    public Angle LastFireAngle { get; private set; }

    public Pistol()
    {
     
    }

    public override void FireInternal(bool alt)
    {
        LaunchProjectile(Holder.ZIndex);
    }

    protected virtual Projectile LaunchProjectile(int z)
    {
        if (this.GetType() == typeof(Pistol))
        {
            Game.Current.Sound.Play("Pistol");
        }
        LastFireAngle = Holder.CalculateAngleToTarget().Add(AngleVariation());
        var bullet = new Projectile(this, Speed, LastFireAngle) { PlaySoundOnImpact = true };
        bullet.MoveTo(bullet.Left, bullet.Top, z);
        bullet.Pen = ProjectilePen ?? bullet.Pen;
        Game.Current.GamePanel.Add(bullet);
        OnWeaponElementEmitted.Fire(bullet);
        return bullet;
    }
}