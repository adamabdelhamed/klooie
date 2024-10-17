namespace ScrollSucker;

public class HP 
{
    public static HP Current { get; private set; }
    private Dictionary<ConsoleControl, HPInfo> HPInfo = new Dictionary<ConsoleControl, HPInfo>();
    public Event<ConsoleControl> HPChanged { get; private set; } = new Event<ConsoleControl>();
    public Event<DamageEnforcementEvent> OnCharacterDestroyed { get; private set; } = new Event<DamageEnforcementEvent>();
    public Event<DamageEnforcementEvent> OnDamageEnforced { get; private set; } = new Event<DamageEnforcementEvent>();

    public HP()
    {
        if (Current != null) throw new Exception("HP already initialized");
        Current = this;
        Game.Current.OnDisposed(() => Current = null);
        Game.Current.MainColliderGroup.OnCollision.Subscribe(ReportCollision, Game.Current);
        OnDamageEnforced.Subscribe(args =>
        {
            if ((args.RawArgs.Damagee as Character)?.IsExpired == true) OnCharacterDestroyed.Fire(args);
        }, Game.Current);
    }

    public void ReportCollision(Collision collision)
    {
        if (collision.MovingObject is Character && collision.MovingObjectSpeed < 50) return;
        if (IsDamageable(collision.ColliderHit) == false) return;
        ReportDamage(new DamageEventArgs(collision.MovingObject, collision.ColliderHit), collision);
    }

    public bool IsDamageable(ConsoleControl el) => el != null && HPInfo.ContainsKey(el);

    public void ReportDamage(DamageEventArgs args, Collision? collision = null)
    {
        if (args.Damagee.ShouldStop || args.Damager.ShouldStop) return;
        if (TryGetDamageInfo(args.Damagee, out HPInfo ignored) == false) return;
        if (IsDamageable(args.Damagee) == false) return;

        var weapon = (args.Damager as WeaponElement)?.Weapon;
        if (weapon == null) return;

        if (weapon.Strength != 0)
        {
            var damageArgs = new DamageEnforcementEvent() { RawArgs = args, DamageAmount = -weapon.Strength, Collision = collision };
            AddHP(args.Damagee, -weapon.Strength);
            OnDamageEnforced.Fire(damageArgs);
        }
    }

    public void AddHP(ConsoleControl element, float amount)
    {
        var currentHp = GetHP(element);
        var newHP = currentHp + amount;
        SetHP(element, newHP);
    }

    public void SetDamageInfo(ConsoleControl element, HPInfo power)
    {
        if (HPInfo.ContainsKey(element) == false)
        {
            HPInfo.Add(element, new HPInfo());
        }
        HPInfo[element].MaxHP = power.MaxHP;
        SetHP(element, power.HP);
    }

    public void SetHP(ConsoleControl element, float newHP)
    {
        newHP = newHP < 0 ? 0 : newHP;
        if (newHP < 0) throw new InvalidOperationException("negative HP detected");

        if (HPInfo.TryGetValue(element, out HPInfo elementPower) == false)
        {
            elementPower = new HPInfo();
            HPInfo.Add(element, elementPower);
        }
        elementPower.MaxHP = float.IsFinite(newHP) == false ? float.PositiveInfinity : newHP > elementPower.MaxHP ? newHP : elementPower.MaxHP;
        var oldHp = elementPower.HP;
        var wasDecrease = oldHp > newHP && float.IsFinite(oldHp);
        newHP = Math.Min(elementPower.MaxHP, newHP);
        elementPower.HP = newHP;

        if (newHP == 0)
        {
            element.Dispose();
        }
        else if (wasDecrease && Game.Current.MainColliderGroup.Now > TimeSpan.FromSeconds(1) && element is Character)
        {
            var update = Game.Current.GamePanel.Controls.WhereAs<HPUpdate>().Where(e => e.Target == element).FirstOrDefault();

            if (update == null)
            {
                update = Game.Current.GamePanel.Add(new HPUpdate(newHP, elementPower.MaxHP, element, newHP - oldHp));
            }
            else
            {
                update.Refresh(newHP, elementPower.MaxHP);
            }
        }
        HPChanged.Fire(element);
    }

    public float GetHP(ConsoleControl element) => HPInfo.TryGetValue(element, out HPInfo elementPower) ? elementPower.HP : float.PositiveInfinity;
    public bool TryGetDamageInfo(ConsoleControl element, out HPInfo info)
    {
        if (HPInfo.TryGetValue(element, out HPInfo elementPower))
        {
            info = elementPower;
            return true;
        }

        info = null;
        return false;
    }

    public HPInfo GetDamageInfo(ConsoleControl element)
    {
        if (TryGetDamageInfo(element, out HPInfo ret) == false) throw new ArgumentException($"Element {element.GetType().Name} has no power");
        return ret;
    }

    public void AbsorbPower(Character absorber, Character other)
    {
        if (other.TryGetDamageInfo(out HPInfo otherInfo) == false) return;

        if (absorber.TryGetDamageInfo(out HPInfo info) == false)
        {
            absorber.SetDamageInfo(new HPInfo() { HP = otherInfo.MaxHP, MaxHP = otherInfo.MaxHP });
            return;
        }

        var oldHp = info.HP;
        info.MaxHP += otherInfo.MaxHP;
        info.HP += otherInfo.MaxHP;
        absorber.SetDamageInfo(info);
        Game.Current.GamePanel.Add(new HPUpdate(info.HP, info.MaxHP, absorber, info.HP - oldHp));
    }
}

public class DamageEnforcementEvent
{
    public DamageEventArgs RawArgs { get; set; }
    public float DamageAmount { get; set; }
    public Collision? Collision { get; set; }
}

public class HPInfo : ObservableObject
{
    public float HP { get => Get<float>(); set => Set(value); }
    public float MaxHP { get => Get<float>(); set => Set(value); }

    public HPInfo()
    {
        HP = float.PositiveInfinity;
        MaxHP = float.PositiveInfinity;
    }
}

public class DamageEventArgs
{
    public GameCollider Damager { get; private set; }
    public GameCollider Damagee { get; private set; }

    public HPInfo DamagerInfo { get; private set; }
    public HPInfo DamageeInfo { get; private set; }

    public DamageEventArgs(ConsoleControl damager, ConsoleControl damagee)
    {
        this.Damager = damager as GameCollider;
        this.Damagee = damagee as GameCollider;

        if (damager.TryGetDamageInfo(out HPInfo d))
        {
            DamagerInfo = d;
        }

        if (damagee.TryGetDamageInfo(out HPInfo d2))
        {
            DamageeInfo = d2;
        }
    }
}
public enum HPMode
{
    Add,
    Set
}

public static class DamageExtensions
{
    public static void SetHP(this ConsoleControl c, float hp) => HP.Current.SetHP(c, hp);

    public static void SetHP(this ConsoleControl c, float hp, float maxHp)
    {
        SetDamageInfo(c, new HPInfo() { HP = hp, MaxHP = maxHp});
    }
    public static void SetDamageInfo(this ConsoleControl c, HPInfo power) => HP.Current.SetDamageInfo(c, power);
    public static void SetDamageInfo(this ConsoleControl c, float hp, float maxHp) => HP.Current.SetDamageInfo(c, new HPInfo()
    {
        HP = hp,
        MaxHP = maxHp,
    });

    public static float GetHP(this ConsoleControl c) => HP.Current.GetHP(c);
    public static float GetMaxHP(this ConsoleControl c) => HP.Current.GetDamageInfo(c).MaxHP;
    public static HPInfo GetDamageInfo(this ConsoleControl c) => HP.Current.GetDamageInfo(c);
    public static bool TryGetDamageInfo(this ConsoleControl c, out HPInfo p) => HP.Current.TryGetDamageInfo(c, out p);
    public static bool IsDamageable(this ConsoleControl c) => HP.Current.IsDamageable(c);
}