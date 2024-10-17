namespace ScrollSucker;

public class Player : Character
{
    private int movementId = 0;
    private float speed;

    public bool InputEnabled { get; set; } = true;

    public Player(float speed, float hp)
    {
        this.speed = speed;
        Foreground = RGB.Black;
        Background = RGB.Green;
        Width = 5;
        Height = 3;
        Velocity.Angle = Angle.Right;
      
        Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.DoNothing;

        Velocity.Speed = speed;

        this.SetDamageInfo(new HPInfo() { HP = hp, MaxHP = hp });

        OverrideTargeting(new AutoTargetingFunction(new AutoTargetingOptions()
        {
            TargetTag = nameof(Enemy),
            Range = float.MaxValue,
            AngularVisibility = 2.5f,
            Source = this,
        }));

        this.Targeting.Delay = 0;


        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.UpArrow, Up, this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.W, Up, this);

        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.DownArrow, Down, this);
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.S, Down, this);

 


        Game.Current.Invoke(async () =>
        {
            var lastPrimaryFireTime = Game.Current.MainColliderGroup.Now;

            Inventory.Subscribe(nameof(Inventory.PrimaryWeapon),()=> lastPrimaryFireTime = TimeSpan.Zero, this);
            while (this.ShouldContinue)
            {
                await Game.Current.Delay(5);
                var primaryCanFire = Game.Current.MainColliderGroup.Now - lastPrimaryFireTime >= TimeSpan.FromMilliseconds(150);

                if (primaryCanFire && this.Inventory.PrimaryWeapon != null && this.Inventory.PrimaryWeapon.AmmoAmount > 0 && this.Target != null)
                {
                    this.Inventory.PrimaryWeapon.TryFire(false);
                    lastPrimaryFireTime = Game.Current.MainColliderGroup.Now;
                }
            }
        });

        Game.Current.PushKeyForLifetime(ConsoleKey.Spacebar, () =>
        {
            this.Inventory.ExplosiveWeapon?.TryFire(false);
        }, this);
    }

    public void Resume()
    {
        Velocity.Angle = Angle.Right;
        Velocity.Speed = speed;
    }

    private void Up()
    {
        if(InputEnabled == false) return;
        Game.Current.Invoke(async () =>
        {
            movementId++;
            var myId = movementId;
            while (this.Top >= Game.Current.GameBounds.Top + 3)
            {
                if (movementId != myId || this.ShouldStop) return;

                if (WillCauseCollision(Bounds.Offset(0, -1)) == false)
                {
                    this.MoveBy(0, -1);
                }
                await Task.Delay(10);
            }

            Game.Current.GamePanel.Children
                .WhereAs<Obstacle>()
                .Where(o => (Game.Current as World).IsTop(o))
                .ForEach(o => o.AddTag(nameof(Enemy)));

            Game.Current.GamePanel.Children
                .WhereAs<Obstacle>()
                .Where(o => (Game.Current as World).IsTop(o) == false)
                .ForEach(o => o.RemoveTag(nameof(Enemy)));
        });
    }

    private bool WillCauseCollision(RectF proposed)
    {
        var collided = false;
        var collidersThatWillHaveACollision = this.GetObstacles()
            .Where(o => o.Touches(proposed))
            .ToArray();

        foreach(var  colliders in collidersThatWillHaveACollision)
        {
            collided = true;
            Velocity.OnCollision.Fire(new Collision()
            {
                Angle = this.CalculateAngleTo(colliders),
                MovingObject = this,
                ColliderHit = colliders,
                MovingObjectSpeed = this.Velocity.Speed,
                Prediction = null,
            });

            colliders.Velocity.OnCollision.Fire(new Collision()
            {
                Angle = colliders.CalculateAngleTo(this),
                MovingObject = colliders,
                ColliderHit = this,
                MovingObjectSpeed = colliders.Velocity.Speed,
                Prediction = null,
            });
        }
        return collided;
    }

    private void Down()
    {
        if (InputEnabled == false) return;
        Game.Current.Invoke(async () =>
        {
            movementId++;
            var myId = movementId;
            while (this.Bottom() <= Game.Current.GameBounds.Bottom - 3)
            {
                if (movementId != myId || this.ShouldStop) return;
                if (WillCauseCollision(Bounds.Offset(0, 1)) == false)
                {
                    this.MoveBy(0, 1);
                }
                await Task.Delay(10);
            }

            Game.Current.GamePanel.Children
                .WhereAs<Obstacle>()
                .Where(o => (Game.Current as World).IsTop(o))
                .ForEach(o => o.RemoveTag(nameof(Enemy)));

            Game.Current.GamePanel.Children
                .WhereAs<Obstacle>()
                .Where(o => (Game.Current as World).IsTop(o) == false)
                .ForEach(o => o.AddTag(nameof(Enemy)));
        });
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        context.FillRect(Background, new Rect(0, 0, Width, Height));

        var stringVal = "";
        if(Inventory.PrimaryWeapon != null && Inventory.PrimaryWeapon.AmmoAmount > 0)
        {
            stringVal = $"{Inventory.PrimaryWeapon.AmmoAmount}";
        }
        else if (Inventory.ExplosiveWeapon != null && Inventory.ExplosiveWeapon.AmmoAmount > 0)
        {
            stringVal = $"{Inventory.ExplosiveWeapon.AmmoAmount}";
        }
        else
        {
            stringVal = "0";
        }

        context.DrawString(stringVal.ToConsoleString(Foreground, Background), ConsoleMath.Round((Width / 2f) - (stringVal.Length / 2f)), ConsoleMath.Round((Height / 2f) - .5f));
    }


}