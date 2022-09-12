namespace klooie.Gaming;


public class Character : GameCollider
{
    public Event<IInteractable> OnInteract { get; private set; } = new Event<IInteractable>();
    public Event<Angle> OnMove { get; private set; } = new Event<Angle>();
    public char? Symbol { get; set; }
    public Inventory Inventory { get => Get<Inventory>(); set => Set(value); }
    public OffenseOptions OffenseOptions { get; protected set; }
    public float MaxMovementSpeed { get; set; } = 20;
    public float CurrentSpeedPercentage { get; set; } = 1f;
    public float PlayerMovementSpeed => MaxMovementSpeed * CurrentSpeedPercentage;

    public GameCollider Target { get; set; }


    public Angle TargetAngle
    {
        get
        {
            if (FreeAimCursor != null)
            {
                return this.Bounds.Center.CalculateAngleTo(FreeAimCursor.Bounds.Center);
            }
            else
            {
                return Target == null ? Velocity.Angle : CalculateAngleToTarget();
            }
        }
    }

    public bool IsBeingTargeted { get; set; }
    public AutoTargetingFunction Targeting { get; protected set; }
    public Cursor FreeAimCursor { get; set; }
    public AimMode AimMode
    {
        get
        {
            return FreeAimCursor != null ? AimMode.Manual : AimMode.Auto;
        }
    }


    public Character()
    {
        CompositionMode = CompositionMode.BlendBackground;
        IsVisible = true;
        this.Subscribe(nameof(Inventory), () => this.Inventory.Owner = this, this);
        Inventory = new Inventory();
        this.ResizeTo(1, 1);
    }

    public virtual bool CanCollideWith(GameCollider other)
    {
        if (base.CanCollideWith(other) == false) return false;

        var otherHolder = (other as WeaponElement)?.Weapon?.Holder;

        if (otherHolder != null)
        {
            if (otherHolder == this) return false;
        }

        return true;
    }


    public Angle CalculateAngleToTarget()
    {
        var realTarget = Target;

        var angle = realTarget != null ?
            this.Bounds.CalculateAngleTo(realTarget.Bounds) :
            Velocity.Angle;

        if (this == MainCharacter.Current && MainCharacter.Current.FreeAimCursor != null)
        {
            angle = this.Bounds.CalculateAngleTo(MainCharacter.Current.FreeAimCursor.Bounds);
        }

        return angle;
    }

    public void MoveLeft()
    {
        var oldAngle = Velocity.Angle;
        if (FreeAimCursor != null)
        {
            FreeAimCursor.MoveBy(-1, 0);
            return;
        }

        if (Velocity.Angle.RoundAngleToNearest(90) != 180)
        {
            Velocity.Angle = 180;
            Velocity.Speed = Math.Min(Velocity.Speed, PlayerMovementSpeed);
            RoundOff();
        }
        else if (Velocity.Angle.RoundAngleToNearest(90) == 180 && Velocity.Speed > 0)
        {
            Velocity.Speed = 0;
            Velocity.Angle = 180;
        }
        else
        {
            Velocity.Angle = 180;
            Velocity.Speed = PlayerMovementSpeed;
            RoundOff();
        }

        OnMove.Fire(oldAngle);
    }

    public void MoveRight()
    {
        var oldAngle = Velocity.Angle;
        if (FreeAimCursor != null)
        {
            FreeAimCursor.MoveBy(1, 0);
            return;
        }

        if (Velocity.Angle.RoundAngleToNearest(90) != 0)
        {
            Velocity.Angle = 0;
            Velocity.Speed = Math.Min(Velocity.Speed, PlayerMovementSpeed);
            RoundOff();
        }
        else if (Velocity.Angle.RoundAngleToNearest(90) == 0 && Velocity.Speed > 0)
        {
            Velocity.Speed = 0;
            Velocity.Angle = 0;
        }
        else
        {
            Velocity.Angle = 0;
            Velocity.Speed = PlayerMovementSpeed;
            RoundOff();
        }
        OnMove.Fire(oldAngle);
    }

    public void MoveDown()
    {
        var oldAngle = Velocity.Angle;
        if (FreeAimCursor != null)
        {
            FreeAimCursor.MoveBy(0, 1);
            return;
        }

        if (Velocity.Angle.RoundAngleToNearest(90) != 90)
        {
            Velocity.Angle = 90;
            Velocity.Speed = Math.Min(Velocity.Speed, PlayerMovementSpeed);
            RoundOff();
        }
        else if (Velocity.Angle.RoundAngleToNearest(90) == 90 && Velocity.Speed > 0)
        {
            Velocity.Angle = 90;
            Velocity.Speed = 0;
        }
        else
        {
            Velocity.Angle = 90;
            Velocity.Speed = PlayerMovementSpeed;
            RoundOff();
        }

        OnMove.Fire(oldAngle);
    }

    public void MoveUp()
    {
        var oldAngle = Velocity.Angle;
        if (FreeAimCursor != null)
        {
            FreeAimCursor.MoveBy(0, -1);
            return;
        }

        if (Velocity.Angle.RoundAngleToNearest(90) != 270)
        {
            Velocity.Angle = 270;
            Velocity.Speed = Math.Min(Velocity.Speed, PlayerMovementSpeed);
            RoundOff();
        }
        else if (Velocity.Angle.RoundAngleToNearest(90) == 270 && Velocity.Speed > 0)
        {
            Velocity.Angle = 270;
            Velocity.Speed = 0;
        }
        else
        {
            Velocity.Angle = 270;
            Velocity.Speed = PlayerMovementSpeed;
            RoundOff();
        }

        OnMove.Fire(oldAngle);
    }

    public void RoundOff()
    {
        var newLeft = (float)ConsoleMath.Round(this.Left);
        var newTop = (float)ConsoleMath.Round(this.Top);
        if (GetObstacleIfMovedTo(new RectF(newLeft, newTop, Width, Height)) == null)
        {
            this.MoveTo(newLeft, newTop);
        }
    }

    public void RoundOff(float x, float y)
    {
        var newLeft = (float)ConsoleMath.Round(x);
        var newTop = (float)ConsoleMath.Round(y);
        if (GetObstacleIfMovedTo(new RectF(newLeft, newTop, Width, Height)) == null)
        {
            this.MoveTo(newLeft, newTop);
        }
    }

    private void EndFreeAim()
    {
        FreeAimCursor?.Dispose();
        FreeAimCursor = null;
        FirePropertyChanged(nameof(AimMode));
    }

    public void ToggleFreeAim()
    {
        var cursor = FreeAimCursor;
        if (cursor == null)
        {
            FreeAimCursor = new Cursor();
            FreeAimCursor.MoveTo(this.Left, this.Top);

            if (Target != null)
            {
                FreeAimCursor.MoveTo(Target.Left, Target.Top);
            }
            Game.Current.GamePanel.Add(FreeAimCursor);
            Velocity.Stop();
            FirePropertyChanged(nameof(AimMode));
        }
        else
        {
            EndFreeAim();
        }
    }

    public void DisableFreeAim()
    {
        var cursor = FreeAimCursor;
        if (cursor != null)
        {
            ToggleFreeAim();
        }
    }

    public void EnableFreeAim()
    {
        var cursor = FreeAimCursor;
        if (cursor == null)
        {
            ToggleFreeAim();
        }
    }

    protected void InitializeTargeting(AutoTargetingFunction func)
    {
        Targeting = func;
        this.OnDisposed(Targeting.Dispose);

        Targeting.TargetChanged.Subscribe((target) =>
        {
            var oldTarget = this.Target;
            this.Target = FreeAimCursor != null ? null : target;

            if (oldTarget != null && oldTarget.IsExpired == false)
            {
                oldTarget.FirePropertyChanged(nameof(Bounds));
            }

            if (this.Target != null && this.Target.IsExpired == false)
            {
                Target.FirePropertyChanged(nameof(Bounds));
            }
        }, this);
    }

    public void DisableTargeting()
    {
        Targeting?.Dispose();
    }

    public void OverrideTargeting(AutoTargetingFunction func)
    {
        InitializeTargeting(func);
    }

    public void TryInteract()
    {
        if (IsVisible == false) return;

        var interactables = Game.Current.GamePanel.Controls
            .Where(e => e is IInteractable)
            .Select(i => i as IInteractable)
            .Where(i => i.InteractionPoint.CalculateDistanceTo(this.Bounds) <= i.MaxInteractDistance)
            .ToArray();

        foreach (var i in interactables)
        {
            i.Interact(this);
            OnInteract.Fire(i);
        }
    }

}
