namespace klooie.Gaming;

 
public readonly struct UniformGridCell : IEquatable<UniformGridCell>
{
    public readonly int X;
    public readonly int Y;

    public UniformGridCell(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public bool Equals(UniformGridCell other)
    {
        return X == other.X && Y == other.Y;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }
}
public sealed class UniformGrid
{  
    private List<UniformGridCell> cellBuffer = new List<UniformGridCell>();
    private const float _cellSize = 100f;
    private readonly Dictionary<UniformGridCell, ObstacleBuffer> _buckets = new Dictionary<UniformGridCell, ObstacleBuffer>();
    private readonly Dictionary<GameCollider,UniformGridMembershipState> membershipStates = new Dictionary<GameCollider, UniformGridMembershipState>();
 
    private uint _stamp;
    private void LoadCells(RectF b)
    {
        cellBuffer.Clear();
        const float pad = 1f;
        const float epsilon = 0.001f;

        var x0 = (int)MathF.Floor((b.Left - pad) / _cellSize);
        var y0 = (int)MathF.Floor((b.Top - pad) / _cellSize);
        var x1 = (int)MathF.Ceiling((b.Right + pad + epsilon) / _cellSize) - 1;
        var y1 = (int)MathF.Ceiling((b.Bottom + pad + epsilon) / _cellSize) - 1;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                cellBuffer.Add(new UniformGridCell(x,y));
            }
        }
    }

    public bool IsExpired(GameCollider c) => membershipStates.TryGetValue(c, out var state) == false;

    public void Insert(GameCollider obj)
    {
        if(membershipStates.ContainsKey(obj)) throw new InvalidOperationException($"This collider of type {obj.GetType().Name} is already registered in the grid. Disposal Reason: {obj.DisposalReason}");
        membershipStates.Add(obj, UniformGridMembershipState.Create(obj, this));

        // Subscribe UniformGrid to the collider's bounds changes.
        // Note that we subscribe for the lifetime of the collider.
        // As of the time of this commit we can be sure that we always
        // remove the collider from the grid when it is disposed.
        // If that ever changes then we will need to unsubscribe in
        // a different way, possibly by using an observable collection.
        obj.UniformGrid = this; // Set the grid reference on the collider
        obj.BoundsChanged.Subscribe(obj, HandleBoundsChanged, obj);
 

        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = ObstacleBufferPool.Instance.Rent();
            }
            list.WriteableBuffer.Add(obj);
        }
    }

    private static void HandleBoundsChanged(object state)
    {
        var obj = (GameCollider)state;
        obj.UniformGrid.Update(obj);
    }

    private void Remove(GameCollider obj)
    {
        var state = membershipStates[obj];
        membershipStates.Remove(obj);
        state.Dispose();

        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.WriteableBuffer.Remove(obj);
                if (list.WriteableBuffer.Count == 0)
                {
                    _buckets.Remove(cell);
                    list.TryDispose();
                }
            }
        }
    }

    public void Update(GameCollider obj)
    {
        var membershipState = membershipStates[obj];

        // Remove from old cells
        LoadCells(membershipState.PreviousBounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.WriteableBuffer.Remove(obj);
                if (list.WriteableBuffer.Count == 0)
                {
                    _buckets.Remove(cell);
                    list.TryDispose();
                }
            }
        }

        // Add to new cells
        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = ObstacleBufferPool.Instance.Rent();
            }
            list.WriteableBuffer.Add(obj);
        }

        membershipState.PreviousBounds = obj.Bounds;
    }

    public void Query(in RectF area, ObstacleBuffer outputBuffer)
    {
        _stamp++;
        LoadCells(area);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var bucketBuffer))
            {
                for (int j = 0; j < bucketBuffer.WriteableBuffer.Count; j++)
                {
                    var itemFromBucket = bucketBuffer.WriteableBuffer[j];
                    if (itemFromBucket.QueryStamp == _stamp) continue;
                    
                    outputBuffer.WriteableBuffer.Add(itemFromBucket);
                    itemFromBucket.QueryStamp = _stamp;
                }
            }
        }
    }

   

    public void QueryExcept(ObstacleBuffer buffer, GameCollider except)
    {
        _stamp++;
        LoadCells(except.Bounds.Grow(80,40)); // TODO - Hacky, but we grow all colliders to increase the # of obstacles that can be seen.
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.WriteableBuffer.Count; j++)
                {
                    GameCollider? obj = list.WriteableBuffer[j];
                    if (obj != except && obj.QueryStamp != _stamp)
                    {
                        buffer.WriteableBuffer.Add(obj);
                        obj.QueryStamp = _stamp;
                    }
                }
            }
        }
    }

    public void EnumerateAll(ObstacleBuffer buffer)
    {
        _stamp++;
        foreach(var list in _buckets.Values)
        { 
            for (int j = 0; j < list.WriteableBuffer.Count; j++)
            {
                GameCollider? obj = list.WriteableBuffer[j];
                if (obj.QueryStamp != _stamp)
                {
                    buffer.WriteableBuffer.Add(obj);
                    obj.QueryStamp = _stamp;
                }
            }
        }
    }

    private class UniformGridMembershipState : Recyclable
    {
        public GameCollider Collider { get; set; }
        public UniformGrid Grid { get; set; }
        public RectF PreviousBounds { get; set; }
        private static LazyPool<UniformGridMembershipState> pool = new LazyPool<UniformGridMembershipState>(() => new UniformGridMembershipState());
        public static UniformGridMembershipState Create(GameCollider collider, UniformGrid grid)
        {
            var ret = pool.Value.Rent();
            ret.Collider = collider;
            ret.Grid = grid;
            ret.PreviousBounds = collider.Bounds;
            collider.OnDisposed(ret, RemoveFromGrid);
            return ret;
        }

        private static void RemoveFromGrid(UniformGridMembershipState state) => state.Grid.Remove(state.Collider);

        protected override void OnReturn()
        {
            base.OnReturn();
            Collider.UniformGrid = null; // Clear the grid reference
            Collider = null; // Clear the collider reference
            Grid = null; // Clear the grid reference
            PreviousBounds = default; // Clear the previous bounds
        }
    }
}