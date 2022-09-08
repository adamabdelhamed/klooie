using klooie.Gaming.Code;

namespace klooie.Gaming;

public class DamageDirective : EventDrivenDirective
{
    public const int MaxPlayerHP = 100;
    public const string OnEnemyDestroyedEventId = "OnEnemyDestroyed";
    public const string RespawningTag = "Respawning";
    public const string CustomDisposalOnKilledTag = "CustomDispose";

    private Dictionary<ConsoleControl, PowerInfo> HPInfo = new Dictionary<ConsoleControl, PowerInfo>();

    public string RespawnOnKilledEvent { get; set; }

    public List<Func<DamageEventArgs, Impact?, bool>> DamageSuppressors = new List<Func<DamageEventArgs, Impact?, bool>>();


    [ArgIgnore]
    public Event<DamageEnforcementEvent> OnDamageEnforced { get; private set; } = new Event<DamageEnforcementEvent>();


    private Dictionary<ConsoleControl, List<Action<float>>> hpChangeHandlers = new Dictionary<ConsoleControl, List<Action<float>>>();


    [ArgIgnore]
    public Event<IGhost> OnGhostDestroyed { get; private set; } = new Event<IGhost>();

    [ThreadStatic]
    private static DamageDirective _current;
    public static DamageDirective Current  { get => _current; private set => _current = value; }

    public override async Task OnEventFired(object args)
    {
        Current = this;
        Game.Current.OnDisposed(() => Current = null);
        Game.Current.MainColliderGroup.ImpactOccurred.Subscribe(ReportImpact, Game.Current);
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

        if (HPInfo.TryGetValue(args.Damagee, out PowerInfo hpForDamagee) == false) return;

        foreach(var suppressor in DamageSuppressors)
        {
            if(suppressor(args,impact))
            {
                return;
            }
        }
        var damagerStrength = HPInfo.TryGetValue(args.Damager, out PowerInfo p) ? p.Strength : 0;
        var damageAmount = args.Damager is WeaponElement && (args.Damager as WeaponElement)?.Weapon != null ?
            (args.Damager as WeaponElement).Weapon.Strength : damagerStrength;

        if (damageAmount != 0)
        {
            AddHP(args.Damagee, -damageAmount, args.Damager as WeaponElement, impact);
            OnDamageEnforced.Fire(new DamageEnforcementEvent() { RawArgs = args, DamageAmount = -damageAmount, Impact = impact });
        }
    }

    public void AddHP(ConsoleControl element, float amount, WeaponElement responsible = null, Impact? impact = null)
    {
        var currentHp = GetHP(element);
        var newHP = currentHp + amount;
        SetHP(element, newHP, responsible, impact);
    }

    public void SetPower(ConsoleControl element, PowerInfo power)
    {
        SetHP(element, power.HP);
        HPInfo[element].MaxHP = power.MaxHP;
        HPInfo[element].Strength = power.Strength;
    }

    public void SetHP(ConsoleControl element, float newHP, WeaponElement responsible = null, Impact? impact = null)
    {
        newHP = newHP < 0 ? 0 : newHP;

        if (newHP < 0)
        {
            throw new InvalidOperationException("negative HP detected");
        }

        if(HPInfo.TryGetValue(element, out PowerInfo elementPower) == false)
        {
            elementPower = new PowerInfo();
            HPInfo.Add(element, elementPower);
            element.OnDisposed(() => HPInfo.Remove(element));
        }
        elementPower.MaxHP = float.IsFinite(newHP) == false ? float.PositiveInfinity :
                             newHP > elementPower.MaxHP ? newHP : elementPower.MaxHP;
        var oldHp = elementPower.HP;
        var wasDecrease = oldHp > newHP;
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
            DoKnockBackEffectIfAppropriate(responsible, element, impact);

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

    public float GetHP(ConsoleControl element) => HPInfo.TryGetValue(element, out PowerInfo elementPower) ? elementPower.HP : float.PositiveInfinity;
    public bool TryGetPower(ConsoleControl element, out PowerInfo info)
    {
        if(HPInfo.TryGetValue(element, out PowerInfo elementPower))
        {
            info = elementPower;
            return true;
        }
        info = null;
        return false;
    }

    public PowerInfo GetPower(ConsoleControl element)
    {
        if(TryGetPower(element, out PowerInfo ret) == false)
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

    private void DoKnockBackEffectIfAppropriate(WeaponElement responsible, ConsoleControl e, Impact? impact = null)
    {
        var element = e as GameCollider;
        if (element == null) return;
        Angle? angle = null;
        if (impact.HasValue)
        {
            angle = impact.Value.Angle;
        }
        else if (responsible != null)
        {
            angle = responsible.CalculateAngleTo(element.Bounds);
        }

        if (angle.HasValue)
        {
            Game.Current.Invoke(() => DramaticImpact.KnockBackAsync(element.GetRoot(), angle.Value, 30, RGB.Gray, ""));
        }
    }

    public class DamageEnforcementEvent
    {
        public DamageEventArgs RawArgs { get; set; }
        public float DamageAmount { get; set; }
        public Impact? Impact { get; set; }
    }


    public class PowerInfo : ObservableObject
    {
        public float HP { get => Get<float>(); set => Set(value); }
        public float MaxHP { get => Get<float>(); set => Set(value); }
        public float Strength { get => Get<float>(); set => Set(value); }

        public PowerInfo()
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
    public static void SetPower(this ConsoleControl c, DamageDirective.PowerInfo power) => DamageDirective.Current.SetPower(c, power);
    public static void SetPower(this ConsoleControl c, float hp, float maxHp, float? strength = null) => DamageDirective.Current.SetPower(c, new DamageDirective.PowerInfo()
    {
        HP = hp,
        MaxHP = maxHp,
        Strength = strength.HasValue ? strength.Value : hp
    });


    public static float GetHP(this ConsoleControl c) => DamageDirective.Current.GetHP(c);
    public static float GetMaxHP(this ConsoleControl c) => DamageDirective.Current.GetPower(c).MaxHP;
    public static float GetStrength(this ConsoleControl c) => DamageDirective.Current.GetPower(c).Strength;
    public static DamageDirective.PowerInfo GetPower(this ConsoleControl c) => DamageDirective.Current.GetPower(c);
    public static bool TryGetPower(this ConsoleControl c, out DamageDirective.PowerInfo p) => DamageDirective.Current.TryGetPower(c, out p);

    public static bool IsDamageable(this ConsoleControl c) => DamageDirective.Current.IsDamageable(c);
}