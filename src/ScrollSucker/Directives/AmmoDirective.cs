namespace ScrollSucker;

public class AmmoDirective : SpawnDirective
{
    public int Amount { get => observable.Get<int>(); set => observable.Set(value); }

    public override GameCollider Preview(World w)
    {
        var ammoToPlace = Game.Current.GamePanel.Add(new Ammo(Amount));
        ammoToPlace.AddTag("ammo");
        w.Place(ammoToPlace, X, Top);
        ammoToPlace.MoveBy(0, 0, 100);
        return ammoToPlace;
    }
    public override void Render(World w)
    {
        Preview(w);
    }

    public override void ValidateUserInput()
    {
        if (Amount <= 0) Amount = 10;
    }

    public override void RemoveFrom(LevelSpec spec) => spec.Ammo.Remove(this);
    public override void AddTo(LevelSpec spec) => spec.Ammo.Add(this);
}

public class Ammo : PickupItem
{
    protected int amount;
    public Ammo(int amount) : base($"AMMO: {amount}".ToGreen()) 
    {
        this.amount = amount;
    }
    public override bool CanCollideWith(GameCollider other)
    {
        if (other is Player) return true;
        return false;
    }

    protected override void PickedUpBy(Player player)
    {
        if (player.Inventory.PrimaryWeapon != null)
        {
            player.Inventory.PrimaryWeapon.AmmoAmount += amount;
        }
    }
}
