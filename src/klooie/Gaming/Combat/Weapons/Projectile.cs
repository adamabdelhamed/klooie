
namespace klooie.Gaming;
public class Projectile : WeaponElement
{
    private static readonly ConsoleCharacter DefaultPen = new ConsoleCharacter('*', RGB.Red);
    public ConsoleCharacter Pen { get; set; } = DefaultPen;
    public float Range { get; set; } = 150;
    private RectF startLocation;
    public void Bind(Weapon w, float speed, Angle angle)
    {
        base.Bind(w);
        CompositionMode = CompositionMode.BlendBackground;
        Velocity.Angle = angle;
        AddHolderSpeedToProjectileSpeedIfNeeded(speed, angle);
        Place(w, angle);
        Velocity.OnCollision.Subscribe(this, OnCollision, this);
        BoundsChanged.Subscribe(this, EnforceRangeStatic, this);
    }

    private static void EnforceRangeStatic(object me)
    {
        var _this = (me as Projectile)!;
        if (_this.Range > 0 && _this.CalculateDistanceTo(_this.startLocation) > _this.Range)
        {
            _this.TryDispose();
        }
    }

    private void Place(Weapon w, Angle angle)
    {
        this.ResizeTo(1, 1);
        this.MoveTo(w.Source.CenterX() - (Width / 2f), w.Source.CenterY() - (Height / 2f), w.Source.ZIndex);
        var offset = this.RadialOffset(angle, 1, false);
        this.MoveTo(offset.Left, offset.Top);
        startLocation = this.Bounds;
    }

    private void AddHolderSpeedToProjectileSpeedIfNeeded(float speed, Angle angle)
    {
        Velocity.speed = speed;
        if (Weapon.Source.Velocity.Angle.DiffShortest(angle) < 45)
        {
            Velocity.speed += Weapon.Source.Velocity.Speed;
        }
    }

    private static void OnCollision(object me, object collision) =>  (me as Projectile)!.TryDispose();
    protected override void OnPaint(ConsoleBitmap context) => context.Fill(Pen);
}