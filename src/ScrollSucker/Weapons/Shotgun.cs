
namespace ScrollSucker;
public class Shotgun : Weapon
{
    public override WeaponStyle Style => WeaponStyle.Explosive;


    public override void FireInternal(bool alt)
    {
        var baseAngle = Holder.TargetAngle;
        var endOfBarrel = Holder.Center().RadialOffset(baseAngle, 1);
        

        ConsoleApp.Current.Sound.Play("shotgun");
        var sprayAngle = ConsoleMath.NormalizeQuantity(27f, baseAngle, reverse: true);
        var startAngle = baseAngle.Add(-sprayAngle / 2);


        var offset = 0f;
        while (offset < sprayAngle)
        {
            var myAngle = startAngle.Add(offset);
            var amount = -(baseAngle.DiffShortest(myAngle));
            var s = 400 + amount;
            var p = Game.Current.GamePanel.Add(new Projectile(this, s, myAngle, autoLocate: false)
            {
                Range = 75,
                Pen = "o".ToGray()
            });

            p.MoveTo(endOfBarrel.Left, endOfBarrel.Top, Holder.ZIndex);
            offset += 3f;
        }
    }
}
