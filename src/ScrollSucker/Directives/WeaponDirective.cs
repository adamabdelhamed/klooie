
namespace ScrollSucker;
using System.Reflection;

public class WeaponDirective : SpawnDirective
{
    public string Type { get => observable.Get<string>(); set => observable.Set(value); }
    public int Ammo { get => observable.Get<int>(); set => observable.Set(value); }
    public float Strength { get => observable.Get<float>(); set => observable.Set(value); }

    public float BurstWindow { get => observable.Get<float>(); set => observable.Set(value); }
    public int MinShotsBeforeEnforced { get => observable.Get<int>(); set => observable.Set(value); }
    public int MaxShotsInBurstWindow { get => observable.Get<int>(); set => observable.Set(value); }

    public override void ValidateUserInput()
    {
        var hasWeapon = Assembly.GetExecutingAssembly()
          .GetTypes()
          .Where(t => t.IsAbstract == false && t.IsSubclassOf(typeof(Weapon)))
          .Where(t => t.Name == Type)
          .Select(t =>
          {
              var ret = Activator.CreateInstance(t) as Weapon;
              ret.AmmoAmount = Ammo;
              ret.Strength = Strength;
              return ret;
          }).Any();

        if (hasWeapon == false) Type = nameof(Pistol);
        if (Ammo <= 0) Ammo = 10;
        if (Strength <= 0) Strength = 10;
    }

    public override GameCollider Preview(World w)
    {
        var weapon = Assembly.GetExecutingAssembly()
           .GetTypes()
           .Where(t => t.IsAbstract == false && t.IsSubclassOf(typeof(Weapon)))
           .Where(t => t.Name == Type)
           .Select(t =>
           {
               var ret = Activator.CreateInstance(t) as Weapon;
               ret.AmmoAmount = Ammo;
               ret.Strength = Strength;
               return ret;
           }).Single();

        weapon.Trigger = new SmartTrigger(BurstWindow, MinShotsBeforeEnforced, MaxShotsInBurstWindow);

        var item = Game.Current.GamePanel.Add(new WeaponItem(weapon));
        w.Place(item, X, Top);
        return item;
    }
    public override void Render(World w)
    {
        Preview(w);
    }

    public override void RemoveFrom(LevelSpec spec) => spec.Weapons.Remove(this);
    public override void AddTo(LevelSpec spec) => spec.Weapons.Add(this);
}

public class WeaponItem : PickupItem
{
    private Weapon w;
    public WeaponItem(Weapon w) : base(w.GetType().Name.ToGreen())
    {
        this.w = w;
    }

    public override bool CanCollideWith(GameCollider other)
    {
        if (other is Player) return true;
        return false;
    }

    protected override void PickedUpBy(Player player)
    {
        player.Inventory.Items.Add(w);
        if (w.Style == WeaponStyle.Primary)
        {
            player.Inventory.PrimaryWeapon = w;
        }
        else if (w.Style == WeaponStyle.Explosive)
        {
            player.Inventory.ExplosiveWeapon = w;
        }
    }
}