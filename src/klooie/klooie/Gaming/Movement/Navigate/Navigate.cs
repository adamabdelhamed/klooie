namespace klooie.Gaming;
public class NavigateOptions
{
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public bool ForceDestination { get; set; } = true;
    public bool Show { get; set; }
    public Func<Task> OnDelay { get; set; }
    public Action OnSuccess { get; set; }
}

public class Navigate : Movement
{
    public ICollider effectiveDestination { get; set; }
    public ICollider _LocalTarget { get; set; }
    public NavigationPath _CurrentPath { get; set; }
    public Lifetime _ResultLifetime { get; private set; } = Game.Current.CreateChildLifetime();
    public RateGovernor _TargetUpdateRateGovernor { get; set; } = new RateGovernor(TimeSpan.FromSeconds(.2));

    private Func<ICollider> destination;
    public NavigateOptions Options { get; private set; }

    public List<RectF> ObstaclesPadded => Velocity
        .GetObstaclesSlow()
            .Select(e => e.Bounds.Grow(.1f))
            .ToList();

    private Navigate(Velocity v, SpeedEval speed, Func<ICollider> destination, NavigateOptions options) : base(v, speed)
    {
        this.Options = options ?? new NavigateOptions();
        this.destination = destination;
    }

    public static Movement Create(Velocity v, SpeedEval speed, Func<ICollider> destination, NavigateOptions options = null) => new Navigate(v, speed, destination, options);

    protected override async Task Move()
    {
        ConfigureCleanup();
        _LocalTarget = destination();

        await Mover.InvokeOrTimeout(this, Wander.Create(Velocity, Speed, new WanderOptions()
        {
            OnDelay = () => Delay(),
            CuriousityPoint = () => _LocalTarget,
        }), EarliestOf(_ResultLifetime, this));
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
            if (_TargetUpdateRateGovernor.ShouldFire(Game.Current.MainColliderGroup.Now))
            {
                var target = _CurrentPath.FindLocalTarget();
                if (target.Equals(_LocalTarget) == false)
                {
                    _LocalTarget = target;
                }
            }
        }
    }

    private async Task EnsurePathUpdated()
    {
        var dest = destination();
        if (dest == null) return;

        if (_CurrentPath == null || _CurrentPath?.IsReallyStuck == true || effectiveDestination == null || effectiveDestination.Bounds.Equals(dest.Bounds) == false)
        {
            effectiveDestination = dest;

            var from = Element.MassBounds;
            var to = effectiveDestination;
            var path = await FindPathAdjusted(from, to.Bounds);
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

    private async Task<List<LocF>> FindPathAdjusted(RectF from, RectF to)
    {
        var sceneW = (int)Game.Current.GameBounds.Width;
        var sceneH = (int)Game.Current.GameBounds.Height;
        var inBounds = new RectF(0, 0, sceneW, sceneH);

        var sceneX = Game.Current.GameBounds.Left;
        var sceneY = Game.Current.GameBounds.Top;

        var adjustedObstacles = ObstaclesPadded
            .Select(o => o.Offset(-sceneX, -sceneY))
            .Where(o => inBounds.Contains(o))
            .ToList();

        from = from.Offset(-sceneX, -sceneY);
        to = to.Offset(-sceneX, -sceneY);
        var path = await AStar.FindPath(sceneW, sceneH, from, to, adjustedObstacles, false);
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

        if(Velocity.Collider.MassBounds.CalculateNormalizedDistanceTo(dest.Bounds) <= Options.CloseEnough)
        {
            if (Options.ForceDestination)
            {
                Velocity.Collider.MoveTo(effectiveDestination.Left(), effectiveDestination.Top());
            }
            ret = true;
        }
        else
        {
            ret = false;
        }
        return ret;
    }
}