namespace klooie.Gaming;
public class NavigateOptions
{
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public bool TryForceDestination { get; set; } = true;
    public bool Show { get; set; }
    public Action OnDelay { get; set; }
    public Action OnSuccess { get; set; }
}

public class Navigate : Movement
{
    public ICollidable effectiveDestination { get; set; }
    public ICollidable _LocalTarget { get; set; }
    public NavigationPath _CurrentPath { get; set; }
    public Recyclable _ResultLifetime { get; private set; }

    private Func<ICollidable> destination;
    public NavigateOptions Options { get; private set; }

    public List<RectF> ObstaclesPadded
    {
        get
        {
            var buffer = ObstacleBufferPool.Instance.Rent();
            try
            {
                Velocity.GetObstacles(buffer);
                var ret = buffer.ReadableBuffer
                    .Select(e => e.Bounds.Grow(.1f))
                    .ToList();
                return ret;
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }
    private void Bind(Velocity v, SpeedEval speed, Func<ICollidable> destination, NavigateOptions options)  
    {
        base.Bind(v, speed);
        AssertSupported();
        _ResultLifetime = Game.Current.CreateChildRecyclable();
        _ResultLifetime.OnDisposed(() => _ResultLifetime = null);
        this.Options = options ?? new NavigateOptions();
        this.destination = destination;
    }

    public static Movement Create(Velocity v, SpeedEval speed, Func<ICollidable> destination, NavigateOptions options = null)
    {
        var ret = NavigatePool.Instance.Rent();
        ret.Bind(v, speed, destination, options);
        return ret;
    }

    protected override async Task Move()
    {
        ConfigureCleanup();
        _LocalTarget = destination();

        await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed, new WanderOptions()
        {
            OnDelay = () => OnDelay(),
            CuriousityPoint = () => _LocalTarget,
        }), EarliestOf(_ResultLifetime, this));

        /*
        Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.DoNothing;
        while(ShouldContinue && _ResultLifetime.ShouldContinue)
        {
            Velocity.Speed = Speed();
            Velocity.Angle = Element.Center().CalculateAngleTo(_LocalTarget.Center());
            Console.WriteLine(Velocity.Angle);
            var delay = Velocity.EvalFrequencySeconds * 1025;
            await Game.Current.Delay(delay);
            await Delay();
        }
        Velocity.Stop();
        */
    }

    private void ConfigureCleanup()
    {
        this.OnDisposed(() => _CurrentPath?.Dispose());
    }

    private float Now => ConsoleMath.Round(Game.Current.MainColliderGroup.Now.TotalSeconds, 1);

    private void OnDelay()
    {
        EnsurePathUpdated();
        Options.OnDelay?.Invoke();

        if (_ResultLifetime == null)
        {
            return;
        }
        else if (HasReachedDestination())
        {
            Options.OnSuccess?.Invoke();
            _ResultLifetime.Dispose();
            return;
        }

        if (_CurrentPath != null)
        {
            _CurrentPath.PruneTail();
            var target = _CurrentPath.FindLocalTarget();
            if (target.Equals(_LocalTarget) == false)
            {
                _LocalTarget = target;
            }
        }
    }

    private void EnsurePathUpdated()
    {
        var dest = destination();
        if (dest == null) return;


        if (_CurrentPath == null || effectiveDestination == null || effectiveDestination.Bounds.Equals(dest.Bounds) == false)
        {
            effectiveDestination = dest;
            AssertSupported();
            var from = Element.Bounds;
            var to = effectiveDestination;
            var path = FindPathAdjusted(from, to.Bounds, ObstaclesPadded);
            var r = path == null ? null : path.Select(l =>new RectF(l.Left, l.Top, 1, 1)).ToList();

            _CurrentPath?.Dispose();
            if (r == null)
            {
                _CurrentPath = null;
                //_Result = false;
                //_ResultLifetime.Dispose();
                return;
            }
            else
            {
                _CurrentPath = new NavigationPath(this, r);
                _LocalTarget = _CurrentPath.FindLocalTarget();
            }
        }
    }

    public static List<LocF> FindPathAdjusted(RectF from, RectF to, IEnumerable<RectF> obstacles)
    {
        var sceneW = (int)Game.Current.GameBounds.Width;
        var sceneH = (int)Game.Current.GameBounds.Height;
        var inBounds = new RectF(0, 0, sceneW, sceneH).ShrinkBy(1,1);

        var sceneX = Game.Current.GameBounds.Left;
        var sceneY = Game.Current.GameBounds.Top;

        var adjustedObstacles = obstacles
            .Select(o => o.Offset(-sceneX, -sceneY))
            .ToList();

        from = from.Offset(-sceneX, -sceneY);
        to = to.Offset(-sceneX, -sceneY);

        if(inBounds.Contains(from) == false || inBounds.Contains(to) == false)
        {
            return null;
        }

        var path = AStar.FindPath(sceneW, sceneH, from, to, adjustedObstacles);
        if (path != null)
        {
            path = path.Select(l => l.Offset(sceneX, sceneY)).ToList();
        }
        return path;
    }

    private bool HasReachedDestination()
    {
        bool ret;
        var dest = destination();

        if (dest == null)
        {
            _ResultLifetime.Dispose();
            return false;
        }

        if(Velocity.Collider.Bounds.CalculateNormalizedDistanceTo(dest.Bounds) <= Options.CloseEnough)
        {
            if (Options.TryForceDestination)
            {
                Velocity.Collider.TryMoveTo(effectiveDestination.Bounds.Left, effectiveDestination.Bounds.Top);
                Velocity.Stop();
            }
            ret = true;
        }
        else
        {
            ret = false;
        }
        return ret;
    }

    private void AssertSupported()
    {
        if (Element.Width > 1 || Element.Height > 1)
        {
            throw new NotSupportedException("Navigate is only supported for 1x1 elements");
        }
    }
}