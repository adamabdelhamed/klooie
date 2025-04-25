namespace klooie.Gaming;

internal sealed class UniformGrid : ISpatialIndex
{
    private const float _cellSize = 20000f;
    private readonly Dictionary<(int x, int y), List<GameCollider>> _buckets = new Dictionary<(int, int), List<GameCollider>>();

    private IEnumerable<(int x, int y)> CellsFor(RectF b)
    {
        const float pad = 1f;
        const float epsilon = 0.001f;

        var x0 = (int)MathF.Floor((b.Left - pad) / _cellSize);
        var y0 = (int)MathF.Floor((b.Top - pad) / _cellSize);
        var x1 = (int)MathF.Ceiling((b.Right + pad + epsilon) / _cellSize) - 1;
        var y1 = (int)MathF.Ceiling((b.Bottom + pad + epsilon) / _cellSize) - 1;

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                yield return (x, y);
    }

    public void Insert(GameCollider obj)
    {
        foreach (var cell in CellsFor(obj.Bounds))
        {
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = new List<GameCollider>();
            }
            list.Add(obj);
        }
    }

    public void Remove(GameCollider obj)
    {
        foreach (var cell in CellsFor(obj.Bounds))
        {
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.Remove(obj);
                if (list.Count == 0)
                {
                    _buckets.Remove(cell);
                }
            }
        }
    }

    public void Update(GameCollider obj, in RectF oldBounds)
    {
        // remove from old cells
        foreach (var cell in CellsFor(oldBounds))
        {
            if (_buckets.TryGetValue(cell, out var list))
            {
                list.Remove(obj);
            }
        }

        // insert into new cells
        foreach (var cell in CellsFor(obj.Bounds))
        {
            if (!_buckets.TryGetValue(cell, out var list))
            {
                _buckets[cell] = list = new List<GameCollider>();
            }
            list.Add(obj);
        }
    }

    public void Query(in RectF area, CollidableBuffer buffer)
    {
        var seen = new HashSet<ICollidable>();
        foreach (var cell in CellsFor(area))
        {
            if (_buckets.TryGetValue(cell, out var list))
            {
                foreach (var obj in list)
                {
                    if (seen.Add(obj))
                    {
                        buffer.Items.Add(obj);
                    }
                }
            }
        }
    }
}
