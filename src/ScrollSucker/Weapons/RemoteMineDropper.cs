namespace ScrollSucker;
public class RemoteMineDropper : Weapon
{
    public override WeaponStyle Style => WeaponStyle.Explosive;

    public override void FireInternal(bool alt)
    {
        var ex = new Explosive(this);
        ProximityMineDropper.PlaceMineSafe(ex, Holder, !alt);
        Game.Current.GamePanel.Add(ex);
        OnWeaponElementEmitted.Fire(ex);
    }

    public static bool Any(Character holder) => Game.Current.GamePanel.Controls
            .WhereAs<Explosive>()
            .Where(e => e.Weapon.Holder == holder)
            .Any();


    public static void DetonateAll(Character holder, float delay = 250)
    {
        var mines = Game.Current.GamePanel.Controls
        .WhereAs<Explosive>()
        .Where(e => e.Weapon.Holder == holder)
        .ToList();
        Game.Current.Invoke(async () =>
        {
            foreach (var mine in mines)
            {
                if (mine.IsExpired == false)
                {
                    mine.Explode();
                    await Game.Current.Delay(delay);
                }
            }
        });
    }
}
