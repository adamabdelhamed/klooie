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
    private const float _cellSize = 30f;
    private readonly Dictionary<UniformGridCell, ObstacleBuffer> _buckets = new Dictionary<UniformGridCell, ObstacleBuffer>();
    private readonly Dictionary<GameCollider,int> leases = new Dictionary<GameCollider, int>();
    private readonly Dictionary<GameCollider, RectF> _previousBounds = new();
 
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

    public bool IsExpired(GameCollider c)
    {
        if (leases.TryGetValue(c, out var lease) == false) return true;
        return c.IsStillValid(lease) == false;
    }

    public void Insert(GameCollider obj)
    {
        leases.Add(obj, obj.Lease);
        _previousBounds[obj] = obj.Bounds;

        // Subscribe UniformGrid to the collider's bounds changes.
        // Note that we subscribe for the lifetime of the collider.
        // As of the time of this commit we can be sure that we always
        // remove the collider from the grid when it is disposed.
        // If that ever changes then we will need to unsubscribe in
        // a different way, possibly by using an observable collection.
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

    private void HandleBoundsChanged(object state)
    {
        var obj = (GameCollider)state;
        Update(obj);
    }

    public void Remove(GameCollider obj)
    {
        leases.Remove(obj);
        _previousBounds.Remove(obj); // Clean up

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
                    list.Dispose();
                }
            }
        }
    }

    public void Update(GameCollider obj)
    {
        if (!_previousBounds.TryGetValue(obj, out var oldBounds))
            oldBounds = obj.Bounds; // fallback, shouldn't happen for registered colliders

        // Remove from old cells
        LoadCells(oldBounds);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.WriteableBuffer.Remove(obj);
                if (list.WriteableBuffer.Count == 0)
                {
                    _buckets.Remove(cell);
                    list.Dispose();
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

        // Update previous bounds
        _previousBounds[obj] = obj.Bounds;
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
}
