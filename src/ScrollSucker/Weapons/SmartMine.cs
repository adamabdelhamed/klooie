namespace ScrollSucker;
public class SmartMine : Weapon
{
    public float Range { get; set; }
    public float AngleIncrement { get; set; }
    public override WeaponStyle Style => WeaponStyle.Explosive;
    public string TargetTag { get; set; } = "enemy";

    public float Speed { get; set; } = 150;

    public SmartMine()
    {
        Strength = 100;
        Range = 30;
    }

    public override void FireInternal(bool alt)
    {
        Game.Current.Sound.Play("thump");
        var mine = new ProximityMine(this) { TargetTag = TargetTag, Range = Range };
        ProximityMineDropper.PlaceMineSafe(mine, Holder, !alt, Speed);
        Game.Current.GamePanel.Add(mine);
        OnWeaponElementEmitted.Fire(mine);
    }
}
