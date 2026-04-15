namespace klooie.Gaming;

public readonly struct UniformGridCell : IEquatable<UniformGridCell>
{
    public readonly int X;
    public readonly int Y;

    public UniformGridCell(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(UniformGridCell other) => X == other.X && Y == other.Y;

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public RectF GetBounds(float cellWidth, float cellHeight)
    {
        float left = X * cellWidth;
        float top = Y * cellHeight;
        return new RectF(left, top, cellWidth, cellHeight);
    }
}

public sealed class UniformGrid
{
    private readonly List<UniformGridCell> cellBuffer = new List<UniformGridCell>();

    private readonly float _cellWidth;
    private readonly float _cellHeight;

    private readonly Dictionary<UniformGridCell, RecyclableList<GameCollider>> _buckets =
        new Dictionary<UniformGridCell, RecyclableList<GameCollider>>(199);

    private readonly Dictionary<GameCollider, UniformGridMembershipState> membershipStates =
        new Dictionary<GameCollider, UniformGridMembershipState>(199);

    public UniformGrid(float cellWidth = 16f, float cellHeight = 8f)
    {
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
    }

    public float CellWidth => _cellWidth;
    public float CellHeight => _cellHeight;

    public readonly struct BucketEnumerable
    {
        private readonly Dictionary<UniformGridCell, RecyclableList<GameCollider>> _d;
        public BucketEnumerable(Dictionary<UniformGridCell, RecyclableList<GameCollider>> d) => _d = d;
        public Dictionary<UniformGridCell, RecyclableList<GameCollider>>.Enumerator GetEnumerator() => _d.GetEnumerator();
    }

    public BucketEnumerable Buckets => new BucketEnumerable(_buckets);

    public int Count { get; private set; }
    private uint _stamp;

    private void LoadCells(in RectF b)
    {
        cellBuffer.Clear();
        const float pad = 1f;
        const float epsilon = 0.001f;

        var x0 = (int)MathF.Floor((b.Left - pad) / _cellWidth);
        var y0 = (int)MathF.Floor((b.Top - pad) / _cellHeight);
        var x1 = (int)MathF.Ceiling((b.Right + pad + epsilon) / _cellWidth) - 1;
        var y1 = (int)MathF.Ceiling((b.Bottom + pad + epsilon) / _cellHeight) - 1;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                cellBuffer.Add(new UniformGridCell(x, y));
            }
        }
    }

    public bool IsExpired(GameCollider c) => membershipStates.TryGetValue(c, out _) == false;

    public void Insert(GameCollider obj)
    {
        if (membershipStates.ContainsKey(obj))
            throw new InvalidOperationException($"This collider of type {obj.GetType().Name} is already registered in the grid. Disposal Reason: {obj.DisposalReason}");

        membershipStates.Add(obj, UniformGridMembershipState.Create(obj, this));

        obj.UniformGrid = this;
        obj.BoundsChanged.SubscribeWithPriority(obj, static objParam => objParam.UniformGrid.Update(objParam), obj);

        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = RecyclableListPool<GameCollider>.Instance.Rent(100);
            }
            list.Items.Add(obj);
        }

        Count++;
    }

    internal void Remove(GameCollider obj)
    {
        var state = membershipStates[obj];
        membershipStates.Remove(obj);
        state.Dispose();

        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.Items.Remove(obj);
                if (list.Items.Count == 0)
                {
                    _buckets.Remove(cell);
                    list.TryDispose("external/klooie/src/klooie/Gaming/Physics/UniformGrid.cs:122");
                }
            }
        }

        Count--;
    }

    public void Update(GameCollider obj)
    {
        var membershipState = membershipStates[obj];

        // Remove from old cells
        LoadCells(membershipState.PreviousBounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.Items.Remove(obj);
                if (list.Items.Count == 0)
                {
                    _buckets.Remove(cell);
                    list.TryDispose("external/klooie/src/klooie/Gaming/Physics/UniformGrid.cs:145");
                }
            }
        }

        // Add to new cells
        LoadCells(obj.Bounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = RecyclableListPool<GameCollider>.Instance.Rent(100);
            }
            list.Items.Add(obj);
        }

        membershipState.PreviousBounds = obj.Bounds;
    }

    public ObstacleBuffer Query(in RectF area, ObstacleBuffer outputBuffer)
    {
        _stamp++;
        LoadCells(area);

        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var bucketBuffer))
            {
                for (int j = 0; j < bucketBuffer.Items.Count; j++)
                {
                    var itemFromBucket = bucketBuffer.Items[j];
                    if (itemFromBucket.QueryStamp == _stamp) continue;

                    outputBuffer.WriteableBuffer.Add(itemFromBucket);
                    itemFromBucket.QueryStamp = _stamp;
                }
            }
        }

        return outputBuffer;
    }

    public void QueryExcept(ObstacleBuffer buffer, GameCollider except)
    {
        _stamp++;
        LoadCells(except.Bounds.Grow(80, 40)); // TODO - Hacky, but we grow all colliders to increase the # of obstacles that can be seen.

        for (int i = 0; i < cellBuffer.Count; i++)
        {
            var cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.Items.Count; j++)
                {
                    var obj = list.Items[j];
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
        foreach (var list in _buckets.Values)
        {
            for (int j = 0; j < list.Items.Count; j++)
            {
                var obj = list.Items[j];
                if (obj.QueryStamp != _stamp)
                {
                    buffer.WriteableBuffer.Add(obj);
                    obj.QueryStamp = _stamp;
                }
            }
        }

    }

    public RectF GetCellBounds(in UniformGridCell cell) => cell.GetBounds(_cellWidth, _cellHeight);

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
            return ret;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Collider.UniformGrid = null;
            Collider = null;
            Grid = null;
            PreviousBounds = default;
        }
    }
}
