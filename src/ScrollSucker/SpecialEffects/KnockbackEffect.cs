namespace ScrollSucker;
public static class KnockBackEffect
{
    public static void Initialize()
    {
        HP.Current.OnDamageEnforced.Subscribe(async (ev) =>
        {
            if (ev.Collision.HasValue == false) return;
            if (ev.RawArgs.Damagee is Obstacle) return;
            await Execute(ev.RawArgs.Damagee, ev.Collision.Value.Angle, ev.Collision.Value.MovingObjectSpeed);
        }, Game.Current);
    }

    public static async Task Execute(GameCollider collider, Angle angle, float speed, float duration = 250)
    {
        speed = speed >= 0 ? speed : 60;
        using (var knockLt = Game.Current.CreateChildLifetime())
        {
            var oldSpeed = collider.Velocity.Speed;
            var oldAngle = collider.Velocity.Angle;
            var oldCollisionBehavior = collider.Velocity.CollisionBehavior;
            knockLt.OnDisposed(() =>
            {
                collider.Velocity.CollisionBehavior = oldCollisionBehavior;
                collider.Velocity.Speed = oldSpeed;
                collider.Velocity.Angle = oldAngle;
            });
            collider.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.DoNothing;

            var temp = new ColliderBox(new RectF());
            collider.Velocity.Angle = angle;
            collider.Velocity.Speed = speed;
            collider.Velocity.BeforeEvaluate.Subscribe(() =>
            {
                collider.Velocity.Angle = angle;
                collider.Velocity.Speed = speed;
            }, knockLt);
            await Game.Current.Delay(duration);
            collider.Velocity.Stop();
        }
    }
}