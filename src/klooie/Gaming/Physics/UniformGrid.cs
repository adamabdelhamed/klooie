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
internal sealed class UniformGrid : ISpatialIndex
{
    private HashSet<int> querySet = new HashSet<int>();
    private List<UniformGridCell> cellBuffer = new List<UniformGridCell>();
    private const float _cellSize = 80f;
    private readonly Dictionary<UniformGridCell, ObstacleBuffer> _buckets = new Dictionary<UniformGridCell, ObstacleBuffer>();
    private readonly Dictionary<GameCollider,int> leases = new Dictionary<GameCollider, int>();

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

    public void Remove(GameCollider obj)
    {
        leases.Remove(obj);
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

    public void Update(GameCollider obj, in RectF oldBounds)
    {
        LoadCells(oldBounds);
        // remove from old cells
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

        // insert into new cells
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

    public void Query(in RectF area, CollidableBuffer buffer)
    {
        querySet.Clear();
        LoadCells(area);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.WriteableBuffer.Count; j++)
                {
                    GameCollider? obj = list.WriteableBuffer[j];
                    if (querySet.Add(obj.ColliderHashCode))
                    {
                        buffer.Items.Add(obj);
                    }
                }
            }
        }
    }

    public void Query(in RectF area, ObstacleBuffer buffer)
    {
        querySet.Clear();
        LoadCells(area);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.WriteableBuffer.Count; j++)
                {
                    GameCollider? obj = list.WriteableBuffer[j];
                    if (querySet.Add(obj.ColliderHashCode))
                    {
                        buffer.WriteableBuffer.Add(obj);
                    }
                }
            }
        }
    }

    public void Query(in RectF area, CollidableBuffer buffer, GameCollider except)
    {
        querySet.Clear();
        LoadCells(area);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.WriteableBuffer.Count; j++)
                {
                    GameCollider? obj = list.WriteableBuffer[j];
                    if (obj != except && querySet.Add(obj.ColliderHashCode))
                    {
                        buffer.Items.Add(obj);
                    }
                }
            }
        }
    }

    public void Query(in RectF area, ObstacleBuffer buffer, GameCollider except)
    {
        querySet.Clear();
        LoadCells(area);
        for (int i = 0; i < cellBuffer.Count; i++)
        {
            UniformGridCell cell = cellBuffer[i];
            if (_buckets.TryGetValue(cell, out var list))
            {
                for (int j = 0; j < list.WriteableBuffer.Count; j++)
                {
                    GameCollider? obj = list.WriteableBuffer[j];
                    if (obj != except && querySet.Add(obj.ColliderHashCode))
                    {
                        buffer.WriteableBuffer.Add(obj);
                    }
                }
            }
        }
    }

    public void EnumerateAll(ObstacleBuffer buffer)
    {
        querySet.Clear();
        foreach(var list in _buckets.Values)
        { 
            for (int j = 0; j < list.WriteableBuffer.Count; j++)
            {
                GameCollider? obj = list.WriteableBuffer[j];
                if (querySet.Add(obj.ColliderHashCode))
                {
                    buffer.WriteableBuffer.Add(obj);
                }
            }
        }
    }
}
