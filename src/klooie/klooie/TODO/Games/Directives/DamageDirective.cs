using klooie.Gaming.Code;

namespace klooie.Gaming;

public class DamageDirective : EventDrivenDirective
{
    public const int MaxPlayerHP = 100;
    public const string OnEnemyDestroyedEventId = "OnEnemyDestroyed";
    public const string RespawningTag = "Respawning";
    public const string DamageableTag = "damageable";
    public const string CustomDisposalOnKilledTag = "CustomDispose";

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
        if (IsDamageable(impact.ColliderHit))
        {
            ReportDamage(new DamageEventArgs()
            {
                Damager = impact.MovingObject as GameCollider,
                Damagee = impact.ColliderHit as GameCollider
            }, impact);
        }
    }

    public bool IsDamageable(ICollider el) => el is GameCollider && (el as GameCollider).HasSimpleTag(DamageableTag);

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

        foreach(var suppressor in DamageSuppressors)
        {
            if(suppressor(args,impact))
            {
                return;
            }
        }

        var damageAmount = args.Damager is WeaponElement && (args.Damager as WeaponElement)?.Weapon != null ?
            (args.Damager as WeaponElement).Weapon.Strength : args.Damager.Power.Strength;

        if (damageAmount != 0)
        {
            AddHP(args.Damagee, -damageAmount, args.Damager as WeaponElement, impact);
            OnDamageEnforced.Fire(new DamageEnforcementEvent() { RawArgs = args, DamageAmount = -damageAmount, Impact = impact });
        }
    }

    public void AddHP(GameCollider element, float amount, WeaponElement responsible = null, Impact? impact = null)
    {
        var currentHp = GetHP(element);
        var newHP = currentHp + amount;
        SetHP(element, newHP, responsible, impact);
    }

    public void SetHP(GameCollider element, float newHP, WeaponElement responsible = null, Impact? impact = null)
    {
        newHP = newHP < 0 ? 0 : newHP;

        if (newHP < 0)
        {
            throw new InvalidOperationException("negative HP detected");
        }

        var oldHp = element.Power.HP;
        var wasDecrease = oldHp > newHP;
        newHP = Math.Min(element.Power.MaxHP, newHP);
        element.Power.HP = newHP;

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
                    element.Power.HP = 0;
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
                update = Game.Current.GamePanel.Add(new HPUpdate(newHP, element.Power.MaxHP, element, newHP - oldHp));
            }
            else
            {
                update.Refresh(newHP, element.Power.MaxHP);
            }
        }
    }

    public float GetHP(GameCollider element) => element.Power != null ? element.Power.HP : float.PositiveInfinity;


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

    private void DoKnockBackEffectIfAppropriate(WeaponElement responsible, GameCollider element, Impact? impact = null)
    {
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
    public GameCollider Target { get; set; }

    private DateTime removeTime;
    public HPUpdate(float current, float max, GameCollider target, float delta)
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


