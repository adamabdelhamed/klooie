namespace klooie.Gaming;
public partial class Weapon : Recyclable, IObservableObject
{
    private static Event<Weapon>? onFire;
    public static Event<Weapon> OnFire 
    {
        get
        {
            if (onFire == null)
            {
                onFire = Event<Weapon>.Create();
                Game.Current.OnDisposed(()=>
                {
                    onFire.Dispose();
                    onFire = null;
                });
            }
            return onFire;
        }
    }

    private Throttle? Debouncer { get; set; }
    public Targeting Targeting { get; private set; }
    public GameCollider Source => Targeting?.Vision?.Eye;
    public partial int AmmoAmount { get; set; }
    private TimeSpan? lastFireTime;
    protected TimeSpan MinTimeBetweenShots { get; set; } = TimeSpan.FromSeconds(.1);

    protected override void OnInit()
    {
        base.OnInit();
        AmmoAmount = -1;
        Targeting = null;
        lastFireTime = null;
    }

    public virtual void Bind(Targeting targeting)
    {
        if (Targeting != null) throw new InvalidOperationException("Targeting already bound");
        this.Targeting = targeting;
    }

    public Angle CalculateAngleToTarget() => Targeting.Target != null ?
            Source.Bounds.CalculateAngleTo(Targeting.Target.Bounds) :
            Source.Velocity.Angle;

    public void TryFire(bool alt)
    {
        if (Debouncer != null && Debouncer.AllowFire() == false) return;
        if (lastFireTime.HasValue && Game.Current.MainColliderGroup.WallClockNow - lastFireTime < MinTimeBetweenShots) return;
        if (AmmoAmount == 0 || Source == null) return;

        lastFireTime = Game.Current.MainColliderGroup.WallClockNow;
        FrameDebugger.RegisterTask("FireWeapon");
        FireInternal(alt);
        AmmoAmount--;
        OnFire.Fire(this);
    }

    public virtual void FireInternal(bool alt) { }

    public class Throttle
    {
        private float burstWindow;

        private int minShotsBeforeEnforced;
        private int maxShotsInBurstWindow;
        private LinkedList<TimeSpan> burstRecord = new LinkedList<TimeSpan>();
        public Throttle(float burstWindow = 1000, int minShotsBeforeEnforced = 3, int maxShotsInBurstWindow = 5)
        {
            this.burstWindow = burstWindow;
            this.minShotsBeforeEnforced = minShotsBeforeEnforced;
            this.maxShotsInBurstWindow = maxShotsInBurstWindow;
        }

        public bool AllowFire()
        {
            Prune();
            if (minShotsBeforeEnforced > burstRecord.Count || burstRecord.Count < maxShotsInBurstWindow)
            {
                burstRecord.AddLast(Game.Current.MainColliderGroup.WallClockNow);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void Prune()
        {
            var current = burstRecord.First;
            while (current != null && Game.Current.MainColliderGroup.WallClockNow - current.Value > TimeSpan.FromMilliseconds(burstWindow))
            {
                burstRecord.RemoveFirst();
                current = burstRecord.First;
            }
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Targeting = null;
    }

}

public class WeaponElement : GameCollider
{
    public Weapon Weapon { get; private set; }
    protected override void OnInit()
    {
        base.OnInit();
        Weapon = null;
        ZIndex = 0;
    }

    public void Bind(Weapon w)
    {
        if(Weapon != null) throw new InvalidOperationException("Weapon already bound");
        this.Weapon = w;
        this.ZIndex = w.Source?.ZIndex ?? 0;
    }

    public override bool CanCollideWith(ICollidable other)
    {
        if(this.Weapon == null) throw new InvalidOperationException("Weapon is null");
        if (base.CanCollideWith(other) == false) return false;
        if (other == Weapon.Source) return false;
        if ((other as WeaponElement)?.Weapon?.Source == Weapon.Source) return false;
        return true;
    }
}

public class Trigger : Recyclable
{
    private bool bound = false;
    public void Bind(Weapon w, ConsoleKeyInfo main, ConsoleKeyInfo alt)
    {
        if(bound) throw new InvalidOperationException("Trigger already bound");
        bound = true;
        Game.Current.PushKeyForLifetime(main.Key, main.Modifiers, this, (me, k) => w.TryFire(false), this);
        Game.Current.PushKeyForLifetime(alt.Key, alt.Modifiers, this, (me, k) => w.TryFire(true), this);
    }

    protected override void OnInit()
    {
        base.OnInit();
        bound = false;
    }
}