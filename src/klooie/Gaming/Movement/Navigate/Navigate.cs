namespace klooie.Gaming;
public class NavigateOptions
{
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public bool TryForceDestination { get; set; } = true;
    public bool Show { get; set; }
    public Func<Task> OnDelay { get; set; }
    public Action OnSuccess { get; set; }
}

public class Navigate : Movement
{
    public GameCollider effectiveDestination { get; set; }
    public GameCollider _LocalTarget { get; set; }
    public NavigationPath _CurrentPath { get; set; }
    public ILifetime _ResultLifetime { get; private set; } = Game.Current.CreateChildLifetime();

    private Func<GameCollider> destination;
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
                ObstacleBufferPool.Instance.Return(buffer);
            }
        }
    }
    private Navigate(Velocity v, SpeedEval speed, Func<GameCollider> destination, NavigateOptions options) : base(v, speed)
    {
        AssertSupported();
        this.Options = options ?? new NavigateOptions();
        this.destination = destination;
    }

    public static Movement Create(Velocity v, SpeedEval speed, Func<GameCollider> destination, NavigateOptions options = null) => new Navigate(v, speed, destination, options);

    protected override async Task Move()
    {
        ConfigureCleanup();
        _LocalTarget = destination();

        await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed, new WanderOptions()
        {
            OnDelay = () => Delay(),
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

    private async Task Delay()
    {
        await EnsurePathUpdated();
        if (Options.OnDelay != null)
        {
            await Options.OnDelay();
        }

        if (_ResultLifetime.IsExpired)
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

    private async Task EnsurePathUpdated()
    {
        var dest = destination();
        if (dest == null) return;


        if (_CurrentPath == null || effectiveDestination == null || effectiveDestination.Bounds.Equals(dest.Bounds) == false)
        {
            effectiveDestination = dest;
            AssertSupported();
            var from = Element.Bounds;
            var to = effectiveDestination;
            var path = await FindPathAdjusted(from, to.Bounds, ObstaclesPadded);
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

    public static async Task<List<LocF>> FindPathAdjusted(RectF from, RectF to, IEnumerable<RectF> obstacles)
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
                Velocity.Collider.TryMoveTo(effectiveDestination.Left, effectiveDestination.Top);
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