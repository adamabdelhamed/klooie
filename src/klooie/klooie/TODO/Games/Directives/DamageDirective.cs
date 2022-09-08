using klooie.Gaming.Code;

namespace klooie.Gaming;

public class DamageDirective : Directive
{
    public const int MaxPlayerHP = 100;
    public const string OnEnemyDestroyedEventId = "OnEnemyDestroyed";
    public const string RespawningTag = "Respawning";
    public const string CustomDisposalOnKilledTag = "CustomDispose";

    private Dictionary<ConsoleControl, DamageInfo> HPInfo = new Dictionary<ConsoleControl, DamageInfo>();

    public string RespawnOnKilledEvent { get; set; }

    public List<Func<DamageEventArgs, Impact?, bool>> DamageSuppressors = new List<Func<DamageEventArgs, Impact?, bool>>();

    [ArgIgnore]
    public Event<DamageEnforcementEvent> OnEnemyDestroyed { get; private set; } = new Event<DamageEnforcementEvent>();

    [ArgIgnore]
    public Event<DamageEnforcementEvent> OnDamageEnforced { get; private set; } = new Event<DamageEnforcementEvent>();
 

    private Dictionary<ConsoleControl, List<Action<float>>> hpChangeHandlers = new Dictionary<ConsoleControl, List<Action<float>>>();


    [ArgIgnore]
    public Event<IGhost> OnGhostDestroyed { get; private set; } = new Event<IGhost>();

    [ThreadStatic]
    private static DamageDirective _current;
    public static DamageDirective Current  { get => _current; private set => _current = value; }

    public override Task ExecuteAsync()
    {
        Current = this;
        Game.Current.OnDisposed(() => Current = null);
        Game.Current.MainColliderGroup.ImpactOccurred.Subscribe(ReportImpact, Game.Current);
        return Task.CompletedTask;
    }
       
    public void ReportImpact(Impact impact)
    {
        if (IsDamageable(impact.ColliderHit as ConsoleControl))
        {
            ReportDamage(new DamageEventArgs()
            {
                Damager = impact.MovingObject as GameCollider,
                Damagee = impact.ColliderHit as GameCollider
            }, impact);
        }
    }

    public bool IsDamageable(ConsoleControl el) => el != null && HPInfo.ContainsKey(el);

    public void ReportDamage(DamageEventArgs args, Impact? impact = null)
    {
        var damagerIsEnemy = args.Damager is WeaponElement &&
            (args.Damager as WeaponElement).Weapon != null &&
            (args.Damager as WeaponElement).Weapon.Holder != null &&
            (args.Damager as WeaponElement).Weapon.Holder.HasSimpleTag("enemy");

        if (damagerIsEnemy && args.Damagee.HasSimpleTag("enemy"))
        {
            // no friendly fire for NPCs
            return;
        }

        if (HPInfo.TryGetValue(args.Damagee, out DamageInfo hpForDamagee) == false) return;

        foreach(var suppressor in DamageSuppressors)
        {
            if(suppressor(args,impact))
            {
                return;
            }
        }
        var damagerStrength = HPInfo.TryGetValue(args.Damager, out DamageInfo p) ? p.Strength : 0;
        var damageAmount = args.Damager is WeaponElement && (args.Damager as WeaponElement)?.Weapon != null ?
            (args.Damager as WeaponElement).Weapon.Strength : damagerStrength;

        if (damageAmount != 0)
        {
            var damageArgs = new DamageEnforcementEvent() { RawArgs = args, DamageAmount = -damageAmount, Impact = impact };
            AddHP(args.Damagee, -damageAmount, args.Damager as WeaponElement, impact, damageArgs);
            OnDamageEnforced.Fire(damageArgs);
        }
    }

    public void AddHP(ConsoleControl element, float amount, WeaponElement responsible = null, Impact? impact = null, DamageEnforcementEvent args = null)
    {
        var currentHp = GetHP(element);
        var newHP = currentHp + amount;
        SetHP(element, newHP, responsible, impact, args);
    }

    public void SetDamageInfo(ConsoleControl element, DamageInfo power)
    {
        SetHP(element, power.HP);
        HPInfo[element].MaxHP = power.MaxHP;
        HPInfo[element].Strength = power.Strength;
    }

    public void SetHP(ConsoleControl element, float newHP, WeaponElement responsible = null, Impact? impact = null, DamageEnforcementEvent args = null)
    {
        newHP = newHP < 0 ? 0 : newHP;

        if (newHP < 0)
        {
            throw new InvalidOperationException("negative HP detected");
        }

        if(HPInfo.TryGetValue(element, out DamageInfo elementPower) == false)
        {
            elementPower = new DamageInfo();
            HPInfo.Add(element, elementPower);
            element.OnDisposed(() => HPInfo.Remove(element));
        }
        elementPower.MaxHP = float.IsFinite(newHP) == false ? float.PositiveInfinity :
                             newHP > elementPower.MaxHP ? newHP : elementPower.MaxHP;
        var oldHp = elementPower.HP;
        var wasDecrease = oldHp > newHP && float.IsFinite(oldHp);
        newHP = Math.Min(elementPower.MaxHP, newHP);
        elementPower.HP = newHP;

        if (hpChangeHandlers.TryGetValue(element, out List<Action<float>> handlers))
        {
            foreach (var handler in handlers)
            {
                handler(newHP);
            }
        }

        if (element.Id != null)
        {
            Game.Current.RuleVariables.Set(newHP, element.Id + "HP");
        }

        if (newHP == 0)
        {
            if (element.HasSimpleTag("enemy"))
            {
                if(args != null) OnEnemyDestroyed.Fire(args);
                Game.Current.Publish(OnEnemyDestroyedEventId, element);
            }
            if (element is IGhost)
            {
                (element as IGhost).IsGhost = true;
                OnGhostDestroyed.Fire(element as IGhost);
            }
            else if (element is MainCharacter && RespawnOnKilledEvent != null)
            {
                if (element.HasSimpleTag(RespawningTag) == false)
                {
                    element.AddTag(RespawningTag);
                    elementPower.HP = 0;
                    Game.Current.Publish(RespawnOnKilledEvent);
                }
            }
            else
            {
                if (element.HasSimpleTag(CustomDisposalOnKilledTag))
                {
                    Game.Current.Publish(element.Id + "CustomDispose");
                }
                else
                {
                    element.Dispose();
                }
            }
        }
        else if(wasDecrease)
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
    }

    public float GetHP(ConsoleControl element) => HPInfo.TryGetValue(element, out DamageInfo elementPower) ? elementPower.HP : float.PositiveInfinity;
    public bool TryGetDamageInfo(ConsoleControl element, out DamageInfo info)
    {
        if(HPInfo.TryGetValue(element, out DamageInfo elementPower))
        {
            info = elementPower;
            return true;
        }
        info = null;
        return false;
    }

    public DamageInfo GetDamageInfo(ConsoleControl element)
    {
        if(TryGetDamageInfo(element, out DamageInfo ret) == false)
        {
            throw new ArgumentException("Element has no power");
        }
        return ret;
    }

    public void RegisterHPChangedForLifetime(GameCollider element, Action<float> hpChangedHandler, ILifetimeManager lifetime)
    {
        if (hpChangeHandlers.TryGetValue(element, out List<Action<float>> handlers) == false)
        {
            handlers = new List<Action<float>>();
            hpChangeHandlers.Add(element, handlers);
        }

        handlers.Add(hpChangedHandler);

        lifetime.OnDisposed(() =>
        {
            handlers.Remove(hpChangedHandler);
            if (handlers.Count == 0)
            {
                hpChangeHandlers.Remove(element);
            }
        });
    }



    public class DamageEnforcementEvent
    {
        public DamageEventArgs RawArgs { get; set; }
        public float DamageAmount { get; set; }
        public Impact? Impact { get; set; }
    }


    public class DamageInfo : ObservableObject
    {
        public float HP { get => Get<float>(); set => Set(value); }
        public float MaxHP { get => Get<float>(); set => Set(value); }
        public float Strength { get => Get<float>(); set => Set(value); }

        public DamageInfo()
        {
            HP = float.PositiveInfinity;
            MaxHP = float.PositiveInfinity;
            Strength = 0;
        }
    }
}
public class DamageEventArgs
{
    public GameCollider Damager { get; set; }
    public GameCollider Damagee { get; set; }
}


 

public class HPUpdate : GameCollider
{
    public float Percentage { get; private set; }
    public float CurrentHP { get; private set; }
    public ConsoleControl Target { get; set; }

    private DateTime removeTime;
    public HPUpdate(float current, float max, ConsoleControl target, float delta)
    {
        if (target.IsExpired)
        {
            Dispose();
            return;
        }

        if (target is Character && (target as Character).IsVisible == false)
        {
            Dispose();
            return;
        }

        this.Target = target;
        Refresh(current, max);
        this.Width = 10;
        this.Height = 1;
        this.MoveTo(target.Left, target.Top - 2, 1000);
        Subscribe(nameof(Bounds),() =>
        {
            this.MoveTo(target.Left, target.Top - 2, 1000);
        }, EarliestOf(this, target));

        target.OnDisposed(() => TryDispose());

        Game.Current.Invoke(async () =>
        {
            while (this.IsExpired == false)
            {
                await Task.Yield();
                if (removeTime < DateTime.UtcNow)
                {
                    Dispose();
                }
            }
        });
    }

    public void Refresh(float current, float max)
    {
        CurrentHP = current;
        removeTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(1));
        Percentage = current / max;
        FirePropertyChanged(nameof(Bounds));
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        var percentage = Percentage;
        context.Fill(RGB.DarkGray);
        var fill = ConsoleMath.Round(percentage * Width);

        var hp = (int)Math.Ceiling(CurrentHP);
        var hpString = hp.ToString("N0");

        var hpStringLeft = (Width - hpString.Length) / 2;


        if (fill == 0 && Percentage > 0)
        {
            fill = 1;
        }
        else if (fill == Bounds.Width && Percentage < 1)
        {
            // todo - this is not working. The rect is still being filled for some reason.
            fill--;
        }
        var textColor = percentage > .5f ? RGB.Black : RGB.DarkRed;
        var fillColor = percentage > .5f ? RGB.Green : RGB.Red;
        var buffer = new ConsoleCharacter[hpString.Length];
        for (var i = 0; i < hpString.Length; i++)
        {
            var left = hpStringLeft + i;
            buffer[i] = new ConsoleCharacter(hpString[i], textColor, left < fill ? fillColor : RGB.DarkGray);
        }

        context.FillRect(fillColor, 0, 0, fill, (int)Height);
        context.DrawString(new ConsoleString(buffer), hpStringLeft, 0);
    }

}

public static class DamageExtensions
{
    public static void SetHP(this ConsoleControl c, float hp) => DamageDirective.Current.SetHP(c, hp);
    public static void SetDamageInfo(this ConsoleControl c, DamageDirective.DamageInfo power) => DamageDirective.Current.SetDamageInfo(c, power);
    public static void SetDamageInfo(this ConsoleControl c, float hp, float maxHp, float? strength = null) => DamageDirective.Current.SetDamageInfo(c, new DamageDirective.DamageInfo()
    {
        HP = hp,
        MaxHP = maxHp,
        Strength = strength.HasValue ? strength.Value : hp
    });


    public static float GetHP(this ConsoleControl c) => DamageDirective.Current.GetHP(c);
    public static float GetMaxHP(this ConsoleControl c) => DamageDirective.Current.GetDamageInfo(c).MaxHP;
    public static float GetStrength(this ConsoleControl c) => DamageDirective.Current.GetDamageInfo(c).Strength;
    public static DamageDirective.DamageInfo GetDamageInfo(this ConsoleControl c) => DamageDirective.Current.GetDamageInfo(c);
    public static bool TryGetDamageInfo(this ConsoleControl c, out DamageDirective.DamageInfo p) => DamageDirective.Current.TryGetDamageInfo(c, out p);

    public static bool IsDamageable(this ConsoleControl c) => DamageDirective.Current.IsDamageable(c);
}