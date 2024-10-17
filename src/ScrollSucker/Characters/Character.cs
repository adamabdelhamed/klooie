namespace ScrollSucker;
public class Character : GameCollider
{
    public Event<Angle> OnMove { get; private set; } = new Event<Angle>();
    public char? Symbol { get; set; }
    public Inventory Inventory { get => Get<Inventory>(); set => Set(value); }
    public float MaxMovementSpeed { get; set; } = 20;
    public float CurrentSpeedPercentage { get; set; } = 1f;
    public float PlayerMovementSpeed => MaxMovementSpeed * CurrentSpeedPercentage;

    public GameCollider Target { get; set; }

    public Angle TargetAngle => Target == null ? Velocity.Angle : CalculateAngleToTarget();
            
    public bool IsBeingTargeted { get; set; }
    public AutoTargetingFunction Targeting { get; protected set; }


    public Character()
    {
        CompositionMode = CompositionMode.BlendBackground;
        IsVisible = true;
        this.Subscribe(nameof(Inventory), () => this.Inventory.Owner = this, this);
        Inventory = new Inventory();
        this.ResizeTo(1, 1);
    }

    public override bool CanCollideWith(GameCollider other)
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

        return angle;
    }

    public void MoveLeft()
    {
        var oldAngle = Velocity.Angle;

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

    private GameCollider GetObstacleIfMovedTo(RectF area) => Velocity.GetObstacles().Where(c => c.Bounds.Touches(area)).WhereAs<GameCollider>().FirstOrDefault();

    public void RoundOff()
    {
        var newLeft = (float)ConsoleMath.Round(this.Left);
        var newTop = (float)ConsoleMath.Round(this.Top);
        this.TryMoveTo(newLeft, newTop);
    }

    public void RoundOff(float x, float y)
    {
        var newLeft = (float)ConsoleMath.Round(x);
        var newTop = (float)ConsoleMath.Round(y);
        this.TryMoveTo(newLeft, newTop);
    }

    protected void InitializeTargeting(AutoTargetingFunction func)
    {
        Targeting = func;
        this.OnDisposed(Targeting.Dispose);

        Targeting.TargetChanged.Subscribe((target) =>
        {
            var oldTarget = this.Target;
            this.Target = target;

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
}
