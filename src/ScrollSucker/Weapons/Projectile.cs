namespace ScrollSucker;
public class Projectile : WeaponElement
{
    public static Event<Collision> OnAudibleImpact { get; private set; } = new Event<Collision>();
    public ConsoleString Pen { get; set; } = new ConsoleString("*", RGB.Red);
    public float Range { get; set; } = float.MaxValue;
    public bool PlaySoundOnImpact { get; set; }
    private RectF startLocation;
    public Projectile(Weapon w, float speed, Angle angle, bool autoLocate = true) : base(w)
    {
        this.ResizeTo(1, 1);
        CompositionMode = CompositionMode.BlendBackground;
        if (w.Holder.Velocity.Angle.DiffShortest(angle) < 45)
        {
            speed += w.Holder.Velocity.Speed;
        }

        this.MoveTo(w.Holder.CenterX() - (Width / 2f), w.Holder.CenterY() - (Height / 2f), w.Holder.ZIndex);
        var offset = this.RadialOffset(angle, 1, false);
        this.MoveTo(offset.Left, offset.Top);

        startLocation = this.Bounds;

        this.AddTag(Weapon.WeaponTag);
        Velocity.OnCollision.Subscribe(Speed_ImpactOccurred, this);

        Velocity.Speed = speed;
        Velocity.Angle = angle;

        Subscribe(nameof(Bounds), () =>
        {
            if (Range > 0 && this.CalculateDistanceTo(startLocation) > Range)
            {
                this.TryDispose();
            }
        }, this);
    }

    private void Speed_ImpactOccurred(Collision collision)
    {
        if (PlaySoundOnImpact)
        {
            OnAudibleImpact.Fire(collision);
        }

        HP.Current.ReportCollision(collision);
        this.TryDispose();
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var pen = Pen != null && Pen.Length > 0 ? Pen[0] : new ConsoleCharacter('#');
        context.Fill(pen);
    }
}

