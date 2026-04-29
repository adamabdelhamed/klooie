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
    public override bool Equals(object? obj) => obj is UniformGridCell other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (X * 73856093) ^ (Y * 19349663);
        }
    }

    public RectF GetBounds(float cellWidth, float cellHeight)
    {
        float left = X * cellWidth;
        float top = Y * cellHeight;
        return new RectF(left, top, cellWidth, cellHeight);
    }
}

public sealed class UniformGrid
{
    private readonly float _cellWidth;
    private readonly float _cellHeight;

    private readonly Dictionary<UniformGridCell, RecyclableList<GameCollider>> _buckets =
        new Dictionary<UniformGridCell, RecyclableList<GameCollider>>(199);

    private readonly Dictionary<GameCollider, UniformGridMembershipState> membershipStates =
        new Dictionary<GameCollider, UniformGridMembershipState>(199);

    private readonly float _invCellWidth;
    private readonly float _invCellHeight;

    public UniformGrid(float cellWidth = 16f, float cellHeight = 8f)
    {
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _invCellWidth = 1f / cellWidth;
        _invCellHeight = 1f / cellHeight;
    }

    private void GetCellRange(in RectF b, out int x0, out int y0, out int x1, out int y1)
    {
        const float pad = 1f;
        const float epsilon = 0.001f;

        x0 = (int)MathF.Floor((b.Left - pad) * _invCellWidth);
        y0 = (int)MathF.Floor((b.Top - pad) * _invCellHeight);
        x1 = (int)MathF.Ceiling((b.Right + pad + epsilon) * _invCellWidth) - 1;
        y1 = (int)MathF.Ceiling((b.Bottom + pad + epsilon) * _invCellHeight) - 1;
    }

    private static bool SameCellRange(int ax0, int ay0, int ax1, int ay1, int bx0, int by0, int bx1, int by1) => ax0 == bx0 && ay0 == by0 && ax1 == bx1 && ay1 == by1;

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

    public bool IsExpired(GameCollider c) => membershipStates.TryGetValue(c, out _) == false;

    public void Insert(GameCollider obj)
    {
        if (membershipStates.ContainsKey(obj))
            throw new InvalidOperationException($"This collider of type {obj.GetType().Name} is already registered in the grid. Disposal Reason: {obj.DisposalReason}");

        GetCellRange(obj.Bounds, out var x0, out var y0, out var x1, out var y1);
        membershipStates.Add(obj, UniformGridMembershipState.Create(obj, this, x0, y0, x1, y1));

        obj.UniformGrid = this;
        obj.BoundsChanged.SubscribeWithPriority(obj, static objParam => objParam.UniformGrid.Update(objParam), obj);

        AddToCells(obj, x0, y0, x1, y1);

        Count++;
    }

    internal void Remove(GameCollider obj)
    {
        var state = membershipStates[obj];
        membershipStates.Remove(obj);

        RemoveFromCells(obj, state.X0, state.Y0, state.X1, state.Y1);
        state.Dispose("external/klooie/src/klooie/Gaming/Physics/UniformGrid.cs:1");

        Count--;
    }

    public void Update(GameCollider obj)
    {
        var membershipState = membershipStates[obj];
        GetCellRange(obj.Bounds, out var x0, out var y0, out var x1, out var y1);
        if (SameCellRange(membershipState.X0, membershipState.Y0, membershipState.X1, membershipState.Y1, x0, y0, x1, y1))
        {
            membershipState.PreviousBounds = obj.Bounds;
            return;
        }

        RemoveFromCells(obj, membershipState.X0, membershipState.Y0, membershipState.X1, membershipState.Y1);
        AddToCells(obj, x0, y0, x1, y1);

        membershipState.PreviousBounds = obj.Bounds;
        membershipState.X0 = x0;
        membershipState.Y0 = y0;
        membershipState.X1 = x1;
        membershipState.Y1 = y1;
    }

    private void AddToCells(GameCollider obj, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var cell = new UniformGridCell(x, y);
                if (!_buckets.TryGetValue(cell, out var list))
                {
                    _buckets[cell] = list = RecyclableListPool<GameCollider>.Instance.Rent(100);
                }
                list.Items.Add(obj);
            }
        }
    }

    private void RemoveFromCells(GameCollider obj, int x0, int y0, int x1, int y1)
    {
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var cell = new UniformGridCell(x, y);
                if (_buckets.TryGetValue(cell, out var list) == false) continue;

                list.Items.Remove(obj);
                if (list.Items.Count != 0) continue;

                _buckets.Remove(cell);
                list.TryDispose("external/klooie/src/klooie/Gaming/Physics/UniformGrid.cs:1");
            }
        }
    }

    public ObstacleBuffer Query(in RectF area, ObstacleBuffer outputBuffer)
    {
        var stamp = ++_stamp;
        GetCellRange(area, out var x0, out var y0, out var x1, out var y1);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (_buckets.TryGetValue(new UniformGridCell(x, y), out var bucketBuffer) == false) continue;

                var items = bucketBuffer.Items;
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.QueryStamp == stamp) continue;

                    item.QueryStamp = stamp;
                    outputBuffer.WriteableBuffer.Add(item);
                }
            }
        }

        return outputBuffer;
    }

    public void QueryExcept(ObstacleBuffer buffer, GameCollider except)
    {
        var stamp = ++_stamp;
        var area = except.Bounds.Grow(80, 40); // TODO - Hacky, but we grow all colliders to increase the # of obstacles that can be seen.
        GetCellRange(area, out var x0, out var y0, out var x1, out var y1);

        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (_buckets.TryGetValue(new UniformGridCell(x, y), out var bucketBuffer) == false) continue;

                var items = bucketBuffer.Items;
                for (var i = 0; i < items.Count; i++)
                {
                    var obj = items[i];
                    if (obj == except || obj.QueryStamp == stamp) continue;

                    obj.QueryStamp = stamp;
                    buffer.WriteableBuffer.Add(obj);
                }
            }
        }
    }

    public void EnumerateAll(ObstacleBuffer buffer)
    {
        var stamp = ++_stamp;

        foreach (var list in _buckets.Values)
        {
            var items = list.Items;
            for (var i = 0; i < items.Count; i++)
            {
                var obj = items[i];
                if (obj.QueryStamp == stamp) continue;

                obj.QueryStamp = stamp;
                buffer.WriteableBuffer.Add(obj);
            }
        }
    }

    public RectF GetCellBounds(in UniformGridCell cell) => cell.GetBounds(_cellWidth, _cellHeight);

    private class UniformGridMembershipState : Recyclable
    {
        public GameCollider Collider { get; set; }
        public UniformGrid Grid { get; set; }
        public RectF PreviousBounds { get; set; }
        public int X0 { get; set; }
        public int Y0 { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }

        private static LazyPool<UniformGridMembershipState> pool = new LazyPool<UniformGridMembershipState>(() => new UniformGridMembershipState());

        public static UniformGridMembershipState Create(GameCollider collider, UniformGrid grid, int x0, int y0, int x1, int y1)
        {
            var ret = pool.Value.Rent();
            ret.Collider = collider;
            ret.Grid = grid;
            ret.PreviousBounds = collider.Bounds;
            ret.X0 = x0;
            ret.Y0 = y0;
            ret.X1 = x1;
            ret.Y1 = y1;
            return ret;
        }

        protected override void OnReturn()
        {
            base.OnReturn();
            Collider.UniformGrid = null;
            Collider = null;
            Grid = null;
            PreviousBounds = default;
            X0 = 0;
            Y0 = 0;
            X1 = 0;
            Y1 = 0;
        }
    }
}
