using PowerArgs.Samples;

namespace ScrollSucker;

public abstract class PickupItem : StringCharacter
{
    protected PickupItem(ConsoleString display) : base(display)
    {
        Velocity.OnCollision.Subscribe(c =>
        {
            if (c.MovingObject is Player == false && c.ColliderHit is Player == false) return;
            var player = c.MovingObject as Player ?? c.ColliderHit as Player;
            PickedUpBy(player);
            MakeSureThePlayerCantPickupTheOtherOne(player);
            this.Dispose();
        }, this);
    }

    private void MakeSureThePlayerCantPickupTheOtherOne(Player player)
    {
        var otherItem = World.Current.GamePanel
            .Children
            .WhereAs<PickupItem>()
            .Where(item => item != this && Math.Abs(this.Left - item.Left) < 2)
            .FirstOrDefault();

        otherItem?.Dispose();
    }

    protected abstract void PickedUpBy(Player player);

    public override bool CanCollideWith(GameCollider other)
    {
        if (other is Player) return true;
        return false;
    }
}
