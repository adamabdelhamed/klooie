namespace klooie.Gaming;

public enum WeaponStyle
{
    Primary,
    Explosive,
    Shield
}

public class WeaponElement : GameCollider
{
    private static Event<WeaponElement> _added;
    public static Event<WeaponElement> Added { get => _added ?? (_added = new Event<WeaponElement>()); set => _added = value; }

    public Weapon Weapon { get; set; }
    public WeaponElement(Weapon w)
    {
        this.Weapon = w;
        if (w?.Holder != null)
        {
            this.ZIndex = w.Holder.ZIndex;
        }
        _added?.Fire(this);
    }

    public override bool CanCollideWith(GameCollider other)
    {
        if (base.CanCollideWith(other) == false) return false;
        if (other == Weapon.Holder) return false;
        if ((other as WeaponElement)?.Weapon?.Holder == Weapon?.Holder) return false;
        if (Weapon.Holder.ChildColliders.Contains(other)) return false;

        return true;

    }
}

public class NoOpWeapon : Weapon
{
    public override WeaponStyle Style => WeaponStyle.Primary;

    public override void FireInternal(bool alt)
    {

    }
}

public class NoOpWeaponElement : WeaponElement
{
    public NoOpWeaponElement(Weapon w) : base(w)
    {
    }
}

public abstract class Weapon : ObservableObject, IInventoryItem
{
    public virtual float ProjectileSpeedHint => 50;

    public bool AllowMultiple => false;
    public static Event<Weapon> OnFireEmpty { get; private set; } = new Event<Weapon>();
    public static Event<Weapon> OnFire { get; private set; } = new Event<Weapon>();

    public Event OnFired { get; private set; } = new Event();
    public Event<WeaponElement> OnWeaponElementEmitted { get; private set; } = new Event<WeaponElement>();
    public SmartTrigger Trigger { get; set; }
    public const string WeaponTag = "Weapon";
    public Character Holder { get; set; }
    public object Tag { get; set; }
    public abstract WeaponStyle Style { get; }
    public virtual float Strength { get; set; }
    public ConsoleString DisplayName { get; set; }

    public int AmmoAmount
    {
        get { return Get<int>(); }
        set { Set(value); }
    }

    protected TimeSpan MinTimeBetweenShots { get; set; } = TimeSpan.FromSeconds(.05);

    /// <summary>
    /// If a weapon is picked up and it's the highest ranking in the inventory then it will automatically be put into use
    /// </summary>
    public int PowerRanking { get; set; }

    public Weapon()
    {
        DisplayName = GetType().Name.ToConsoleString();
        AmmoAmount = -1;
    }



    private TimeSpan? lastFireTime;

    public void TryFire(bool alt)
    {
        if (Trigger == null || Trigger.AllowFire())
        {
            if ((AmmoAmount > 0 || AmmoAmount == -1) && Holder != null)
            {
                if (lastFireTime.HasValue && Game.Current.MainColliderGroup.Now - lastFireTime < MinTimeBetweenShots)
                {
                    return;
                }
                lastFireTime = Game.Current.MainColliderGroup.Now;

                OnFire.Fire(this);
                OnFired.Fire();
                FireInternal(alt);
                if (AmmoAmount > 0)
                {
                    AmmoAmount--;

                    if (AmmoAmount == 0)
                    {
                        var alternative = Holder.Inventory.Items
                            .WhereAs<Weapon>()
                            .Where(w => w.Style == this.Style && w.AmmoAmount > 0)
                            .OrderByDescending(w => w.PowerRanking)
                            .FirstOrDefault();

                        if (alternative != null)
                        {
                            if (alternative.Style == WeaponStyle.Primary)
                            {
                                Holder.Inventory.PrimaryWeapon = alternative;
                            }
                            else
                            {
                                Holder.Inventory.ExplosiveWeapon = alternative;
                            }
                        }
                    }

                }
            }
            else
            {
                OnFireEmpty.Fire(this);
            }
        }
    }

    public abstract void FireInternal(bool alt);
}