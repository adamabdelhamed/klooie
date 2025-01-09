using System.Buffers;

namespace klooie.Gaming;
public struct Collision
{
    public float MovingObjectSpeed { get; set; }
    public Angle Angle { get; set; }
    public ICollidable MovingObject { get; set; }
    public ICollidable ColliderHit { get; set; }
    public CollisionPrediction Prediction { get; set; }
    public override string ToString() => $"{Prediction.LKGX},{Prediction.LKGY} - {ColliderHit?.GetType().Name}";
}

public sealed class CollisionPrediction
{
    public bool CollisionPredicted { get; set; }
    public RectF ObstacleHitBounds { get; set; }
    public ICollidable ColliderHit { get; set; }
    public float LKGX { get; set; }
    public float LKGY { get; set; }
    public float LKGD { get; set; }
    public float Visibility { get; set; }
    public Edge Edge { get; set; }
    public float IntersectionX { get; set; }
    public float IntersectionY { get; set; }

    public LocF Intersection => new LocF(IntersectionX, IntersectionY);

    internal CollisionPrediction Clear()
    {
        ColliderHit = null;
        ObstacleHitBounds = default;
        Edge = default;
        CollisionPredicted = false;
        return this;
    }
}

public enum CastingMode
{
    SingleRay,
    Rough,
    Precise
}

public static class CollisionDetector
{
    public const float VerySmallNumber = .00001f;

    // We keep one buffer per thread to avoid repeated allocations.
    // Note: This is not thread-safe if multiple threads call these methods simultaneously.
    [ThreadStatic]
    private static Edge[] rayBuffer = null;

    // Similarly, reuse a RectF[] for obstacle bounds so we don't keep allocating in CreateObstaclesFromColliders.
    [ThreadStatic]
    private static RectF[] colliderBoundsBuffer = null;

    /// <summary>
    /// Checks line of sight from 'from' to 'to' using the obstacles from 'from.GetObstacles(buffer)'.
    /// </summary>
    public static bool HasLineOfSight(this Velocity from, ConsoleControl to)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            from.GetObstacles(buffer);
            return HasLineOfSight(from.Collider, to, buffer.WriteableBuffer);
        }
        finally
        {
            ObstacleBufferPool.Instance.Return(buffer);
        }
    }

    public static bool HasLineOfSight<T>(this ConsoleControl from, ConsoleControl to, IList<T> obstacles) where T : ConsoleControl
        => GetLineOfSightObstruction(from, to, obstacles) == null;

    public static bool HasLineOfSight<T>(this ConsoleControl from, RectF to, IList<T> obstacles) where T : ConsoleControl
        => GetLineOfSightObstruction(from, to, obstacles) == null;

    public static bool HasLineOfSight<T>(this RectF from, ConsoleControl to, IList<T> obstacles) where T : ConsoleControl
        => GetLineOfSightObstruction(from, to, obstacles) == null;

    public static bool HasLineOfSight<T>(this RectF from, RectF to, IList<T> obstacles) where T : ConsoleControl
        => GetLineOfSightObstruction(from, to, obstacles) == null;

       

 
    public static ICollidable? GetLineOfSightObstruction<T>(this RectF from, ConsoleControl to, IList<T> obstacles, CastingMode castingMode = CastingMode.Rough) where T : ConsoleControl
        => GetLineOfSightObstruction(new ColliderBox(from), to, obstacles, castingMode);

    public static ICollidable? GetLineOfSightObstruction<T>(this ConsoleControl from, RectF to, IList<T> obstacles, CastingMode castingMode = CastingMode.Rough) where T : ConsoleControl
    {
        var toCollider = ColliderBoxPool.Instance.Rent();
        toCollider.Bounds = to;
        try
        {
            return GetLineOfSightObstruction(from, toCollider, obstacles, castingMode);
        }
        finally
        {
            ColliderBoxPool.Instance.Return(toCollider);
        }
    }

    public static ICollidable? GetLineOfSightObstruction<T>(this RectF from, RectF to, IList<T> obstacles, CastingMode castingMode = CastingMode.Rough) where T : ConsoleControl
    {
        var fromCollider = ColliderBoxPool.Instance.Rent();
       fromCollider.Bounds = from;
        var toCollider = ColliderBoxPool.Instance.Rent();
        toCollider.Bounds = to;
        try
        {
            return GetLineOfSightObstruction<T>(fromCollider, toCollider, obstacles, castingMode);
        }
        finally
        {
            ColliderBoxPool.Instance.Return(fromCollider);
            ColliderBoxPool.Instance.Return(toCollider);
        }
    }

    public static CollisionPrediction Predict(
        ICollidable from,
        Angle angle,
        ICollidable[] colliders,
        float visibility,
        CastingMode castingMode,
        CollisionPrediction toReuse = null,
        List<Edge> edgesHitOutput = null
    )
    {
        int bufferLen = 0;
        for(var i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) break;
            bufferLen++;
        }

        return Predict(from, angle, colliders, visibility, castingMode, bufferLen, toReuse, edgesHitOutput);
    }

    public static CollisionPrediction Predict(
        ICollidable from,
        Angle angle,
        ICollidable[] colliders,
        float visibility,
        CastingMode castingMode,
        int bufferLen,
        CollisionPrediction toReuse = null,
        List<Edge> edgesHitOutput = null
    )
        => Predict(from, CreateObstaclesFromColliders(colliders, bufferLen), angle, colliders, visibility, castingMode, bufferLen, toReuse, edgesHitOutput);


    public static ICollidable? GetLineOfSightObstruction<T>(
        this ICollidable from,
        ICollidable to,
        IList<T> obstacleControls,
        CastingMode castingMode = CastingMode.Rough
    ) where T : ConsoleControl
    {
        var massBounds = from.Bounds;
        // Instead of Union(new[] {to}), we do a simpler combination
        //var colliders = CombineObstaclesWithTo(obstacleControls, to);

        var colliders = ArrayPool<ICollidable>.Shared.Rent(obstacleControls.Count + 1);
        try
        {
            for (var i = 0; i < obstacleControls.Count; i++)
            {
                colliders[i] = obstacleControls[i];
            }
            colliders[obstacleControls.Count - 1] = to;


            var angle = massBounds.CalculateAngleTo(to.Bounds);
            var distance = massBounds.CalculateDistanceTo(to.Bounds);
            var visibility = 3 * distance;
            var prediction = Predict(from, angle, colliders, visibility, castingMode);
            return prediction.CollisionPredicted == false ? null
                : prediction.ColliderHit == to ? null
                : prediction.ColliderHit;
        }
        finally
        {
            ArrayPool<ICollidable>.Shared.Return(colliders);
        }
    }

    public static CollisionPrediction Predict(
        ICollidable from,
        RectF[] obstacles,
        Angle angle,
        ICollidable[] colliders,
        float visibility,
        CastingMode mode,
        int bufferLen,
        CollisionPrediction toReuse,
        List<Edge> edgesHitOutput = null
    )
    {
        var movingObject = from.Bounds;
        var prediction = toReuse?.Clear() ?? new CollisionPrediction();
        prediction.LKGX = movingObject.Left;
        prediction.LKGY = movingObject.Top;
        prediction.Visibility = visibility;

        if (visibility == 0)
        {
            prediction.CollisionPredicted = false;
            return prediction;
        }

        prediction.Visibility = visibility;

        var rayCount = CreateRays(angle, visibility, mode, movingObject);

        var closestIntersectionDistance = float.MaxValue;
        int closestIntersectingObstacleIndex = -1;
        Edge closestEdge = default;
        float closestIntersectionX = 0;
        float closestIntersectionY = 0;
        var len = bufferLen;

        for (var i = 0; i < len; i++)
        {
            ref var obstacle = ref obstacles[i];

            // skip if the same collider
            if (from == colliders[i]) continue;
            if (colliders[i].ShouldStop) continue;

            if (from is GameCollider && colliders[i] is GameCollider)
            {
                var cc = (GameCollider)from;
                var ci = (GameCollider)colliders[i];
                if (!cc.CanCollideWith(ci) || !ci.CanCollideWith(cc)) continue;
            }

            if (visibility < float.MaxValue &&
                RectF.CalculateDistanceTo(movingObject, obstacle) > visibility + VerySmallNumber)
                continue;

            ProcessEdge(
                i, obstacle.TopEdge, rayCount, edgesHitOutput, visibility,
                ref closestIntersectionDistance, ref closestIntersectingObstacleIndex,
                ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);

            ProcessEdge(
                i, obstacle.BottomEdge, rayCount, edgesHitOutput, visibility,
                ref closestIntersectionDistance, ref closestIntersectingObstacleIndex,
                ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);

            ProcessEdge(
                i, obstacle.LeftEdge, rayCount, edgesHitOutput, visibility,
                ref closestIntersectionDistance, ref closestIntersectingObstacleIndex,
                ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);

            ProcessEdge(
                i, obstacle.RightEdge, rayCount, edgesHitOutput, visibility,
                ref closestIntersectionDistance, ref closestIntersectingObstacleIndex,
                ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
        }

        if (closestIntersectingObstacleIndex >= 0)
        {
            prediction.ObstacleHitBounds = obstacles[closestIntersectingObstacleIndex];
            prediction.ColliderHit = colliders == null ? null : colliders[closestIntersectingObstacleIndex];
            prediction.LKGD = closestIntersectionDistance;
            prediction.LKGX = closestIntersectionX;
            prediction.LKGY = closestIntersectionY;
            prediction.CollisionPredicted = true;
            prediction.Edge = closestEdge;
            prediction.IntersectionX = closestIntersectionX;
            prediction.IntersectionY = closestIntersectionY;
        }

        return prediction;
    }

    private static int CreateRays(Angle angle, float visibility, CastingMode mode, RectF movingObject)
    {
        // Ensure our shared (thread-local) buffer is allocated once.
        if (rayBuffer == null)
        {
            // Enough for "Precise" mode with a bit of headroom
            rayBuffer = new Edge[20000];
        }

        var rayCount = 0;

        if (mode == CastingMode.Precise)
        {
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;

            // corners
            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Top, movingObject.Left + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Top, movingObject.Right + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Bottom, movingObject.Left + dx, movingObject.Bottom + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Bottom, movingObject.Right + dx, movingObject.Bottom + dy);

            var granularity = .5f;
            for (var x = movingObject.Left + granularity; x < movingObject.Left + movingObject.Width; x += granularity)
            {
                rayBuffer[rayCount++] = new Edge(x, movingObject.Top, x + dx, movingObject.Top + dy);
                rayBuffer[rayCount++] = new Edge(x, movingObject.Bottom, x + dx, movingObject.Bottom + dy);
            }

            for (var y = movingObject.Top + granularity; y < movingObject.Top + movingObject.Height; y += granularity)
            {
                rayBuffer[rayCount++] = new Edge(movingObject.Left, y, movingObject.Left + dx, y + dy);
                rayBuffer[rayCount++] = new Edge(movingObject.Right, y, movingObject.Right + dx, y + dy);
            }
        }
        else if (mode == CastingMode.Rough)
        {
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;

            // corners
            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Top, movingObject.Left + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Top, movingObject.Right + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Bottom, movingObject.Left + dx, movingObject.Bottom + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Bottom, movingObject.Right + dx, movingObject.Bottom + dy);
            // center
            rayBuffer[rayCount++] = new Edge(movingObject.CenterX, movingObject.CenterY, movingObject.CenterX + dx, movingObject.CenterY + dy);
        }
        else if (mode == CastingMode.SingleRay)
        {
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;
            // single center ray
            rayBuffer[rayCount++] = new Edge(movingObject.CenterX, movingObject.CenterY, movingObject.CenterX + dx, movingObject.CenterY + dy);
        }
        else
        {
            throw new NotSupportedException("Unknown mode: " + mode);
        }

        return rayCount;
    }

    private static void ProcessEdge(
        int i,
        in Edge edge,
        int rayCount,
        List<Edge> edgesHitOutput,
        float visibility,
        ref float closestIntersectionDistance,
        ref int closestIntersectingObstacleIndex,
        ref Edge closestEdge,
        ref float closestIntersectionX,
        ref float closestIntersectionY
    )
    {
        for (var k = 0; k < rayCount; k++)
        {
            var ray = rayBuffer[k];
            if (TryFindIntersectionPoint(ray, edge, out float ix, out float iy))
            {
                edgesHitOutput?.Add(ray);
                var d = LocF.CalculateDistanceTo(ray.X1, ray.Y1, ix, iy) - VerySmallNumber;

                if (d < closestIntersectionDistance && d <= visibility)
                {
                    closestIntersectionDistance = d;
                    closestIntersectingObstacleIndex = i;
                    closestEdge = edge;
                    closestIntersectionX = ix;
                    closestIntersectionY = iy;
                }
            }
        }
    }

    // Updated intersection point method from GPT-4 (already fairly optimized)
    public static bool TryFindIntersectionPoint(in Edge ray, in Edge stationaryEdge, out float x, out float y)
    {
        var x1 = ray.X1;
        var y1 = ray.Y1;
        var x2 = ray.X2;
        var y2 = ray.Y2;

        var x3 = stationaryEdge.X1;
        var y3 = stationaryEdge.Y1;
        var x4 = stationaryEdge.X2;
        var y4 = stationaryEdge.Y2;

        var den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

        if (den == 0)
        {
            // Check if the segments are collinear
            var det = (x1 - x3) * (y2 - y3) - (y1 - y3) * (x2 - x3);
            if (det != 0)
            {
                x = 0;
                y = 0;
                return false;
            }

            // Check if the segments overlap
            if (Math.Max(x1, x2) < Math.Min(x3, x4) ||
                Math.Max(x3, x4) < Math.Min(x1, x2) ||
                Math.Max(y1, y2) < Math.Min(y3, y4) ||
                Math.Max(y3, y4) < Math.Min(y1, y2))
            {
                x = 0;
                y = 0;
                return false;
            }

            // If collinear and overlapping, pick one overlapping point
            if (x2 >= x3 && x1 <= x3 && y2 >= y3 && y1 <= y3)
            {
                x = x3;
                y = y3;
            }
            else
            {
                x = x1;
                y = y1;
            }
            return true;
        }

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;

        x = x1 + t * (x2 - x1);
        y = y1 + t * (y2 - y1);

        // Helper for "between" checks with a small epsilon
        bool between(float a, float b, float c, float eps = 1e-5f)
            => (a - eps <= b && b <= c + eps) || (c - eps <= b && b <= a + eps);

        if (between(x1, x, x2) && between(y1, y, y2) &&
            between(x3, x, x4) && between(y3, y, y4))
        {
            return true;
        }
        else
        {
            x = 0;
            y = 0;
            return false;
        }
    }

    // Reuse the same RectF[] instead of allocating a new one every time
    private static RectF[] CreateObstaclesFromColliders(ICollidable[] colliders, int len)
    {
        // If we haven't allocated or if we need more space, make a new buffer
        if (colliderBoundsBuffer == null || colliderBoundsBuffer.Length < len)
        {
            colliderBoundsBuffer = new RectF[len];
        }

        for (int i = 0; i < len; i++)
        {
            colliderBoundsBuffer[i] = colliders[i].Bounds;
        }

        return colliderBoundsBuffer;
    }
}

