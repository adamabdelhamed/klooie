
namespace ScrollSucker;
public class Explosive : WeaponElement
{
    public static Event<Explosive> OnExplode { get; private set; } = new Event<Explosive>();

    public float AngleIncrement { get; private set; } = 20;
    public float Range { get; set; } = 18;

    public int Penetration { get; set; } = 2;
    public Event Exploded { get; private set; } = new Event();

    private bool hasExploded;
    public Explosive(Weapon w) : base(w)
    {
        OnDisposed(() => Explode());
        Game.Current.MainColliderGroup.OnCollision.Subscribe((collision) =>
        {
            if (collision.MovingObject == this && CausesExplosion(collision.ColliderHit))
            {
                Explode();
            }
            else if (collision.ColliderHit == this && CausesExplosion(collision.MovingObject))
            {
                Explode();
            }
        }, this);
    }

    private bool CausesExplosion(ConsoleControl thingHit)
    {
        if (thingHit is WeaponElement || thingHit is Character)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Explode()
    {
        Game.Current.Invoke(ExplodeAsync);
    }

    public async Task ExplodeAsync()
    {
        if (hasExploded) return;
        hasExploded = true;
        this.TryDispose();

        TakeToll();

        var shrapnelOut = 0;
        if (Game.Current.GamePanel.IsInView(this))
        {
            var shrapnelDisplay = new ConsoleString("*".ToRed());
            for (float angle = 0; angle < 360; angle += AngleIncrement)
            {
                var loc = this.Center().RadialOffset(angle, Range);
                var dest = new RectF(loc.Left - .5f, loc.Top - .5f, 1, 1);
                shrapnelOut++;
                var shrapnel = Game.Current.GamePanel.Add(new Label(shrapnelDisplay) { CompositionMode = CompositionMode.BlendBackground });
                shrapnel.MoveTo(this.Left, this.Top, this.ZIndex + 100);
                Game.Current.Invoke(async () =>
                {
                    await shrapnel.AnimateAsync(new ConsoleControlAnimationOptions()
                    {
                        Destination = () => dest,
                        Duration = 200,
                        EasingFunction = EasingFunctions.Linear
                    });
                    shrapnel.Dispose();
                    shrapnelOut--;
                });
            }
        }

        Exploded.Fire();
        OnExplode.Fire(this);
        while (shrapnelOut > 0)
        {
            await Task.Yield();
        }
    }

    private async void TakeToll()
    {
        for (var i = 0; i < Penetration; i++)
        {
            var obstacles = this.GetObstacles().ToArray();
            var toHit = obstacles
                .Where(e => e.CalculateDistanceTo(this) <= Range &&
                            CollisionDetector.GetLineOfSightObstruction(this, e, obstacles, CastingMode.SingleRay) == null)
                .ToArray();

            foreach (var element in toHit)
            {
                HP.Current.ReportCollision(new Collision()
                {
                    MovingObject = this,
                    ColliderHit = element,
                    Angle = this.Center().CalculateAngleTo(element.Center()),
                });
            }
            await Task.Yield();
        }
    }

    private static readonly ConsoleString DefaultStyle = new ConsoleString(" ", backgroundColor: RGB.DarkYellow);
    protected override void OnPaint(ConsoleBitmap context) => context.DrawString(DefaultStyle, 0, 0);
}
