namespace klooie.Gaming;
public enum HitType
{
    None = 0,
    Obstacle = 1,
}

public struct Impact
{
    public float MovingObjectSpeed { get; set; }
    public Angle Angle { get; set; }
    public ConsoleControl MovingObject { get; set; }
    public ConsoleControl ColliderHit { get; set; }
    public HitType HitType { get; set; }
    public HitPrediction Prediction { get; set; }

    public override string ToString() => $"{Prediction.LKGX},{Prediction.LKGY} - {ColliderHit?.GetType().Name}";
}

public class HitPrediction
{
    public HitType Type { get; set; }
    public RectF ObstacleHitBounds { get; set; }
    public ConsoleControl ColliderHit { get; set; }
    public float LKGX { get; set; }
    public float LKGY { get; set; }
    public float LKGD { get; set; }
    public float Visibility { get; set; }
    public Edge Edge { get; set; }
    public float IntersectionX { get; set; }
    public float IntersectionY { get; set; }

    public LocF Intersection => new LocF(IntersectionX, IntersectionY);

    internal HitPrediction Clear()
    {
        ColliderHit = null;
        ObstacleHitBounds = default;
        Edge = default;
        Type = HitType.None;
        return this;
    }
}

public enum CastingMode
{
    SingleRay,
    Rough,
    Precise
}

public static class HitDetection
{
    public const float VerySmallNumber = .00001f;

    [ThreadStatic]
    private static Edge[] rayBuffer;

    public static bool HasLineOfSight(this Velocity from, ConsoleControl to) 
        => HasLineOfSight(from.Collider, to, from.GetObstaclesSlow());
    public static bool HasLineOfSight(this ConsoleControl from, ConsoleControl to, IEnumerable<ConsoleControl> obstacles) 
        => GetLineOfSightObstruction(from, to, obstacles) == null;
    public static bool HasLineOfSight(this ConsoleControl from, RectF to, IEnumerable<ConsoleControl> obstacles) 
        => GetLineOfSightObstruction(from, to, obstacles) == null;
    public static bool HasLineOfSight(this RectF from, ConsoleControl to, IEnumerable<ConsoleControl> obstacles) 
        => GetLineOfSightObstruction(from, to, obstacles) == null;
    public static bool HasLineOfSight(this RectF from, RectF to, IEnumerable<ConsoleControl> obstacles) 
        => GetLineOfSightObstruction(from, to, obstacles) == null;
    public static bool HasLineOfSight(this RectF from, RectF to, IEnumerable<RectF> obstacles) 
        => GetLineOfSightObstruction(from, to, obstacles.Select(o => new ColliderBox(o))) == null;
    public static ConsoleControl? GetLineOfSightObstruction(this RectF from, ConsoleControl to, IEnumerable<ConsoleControl> obstacles, CastingMode castingMode = CastingMode.Rough) 
        => GetLineOfSightObstruction(new ColliderBox(from), to, obstacles, castingMode);
    public static ConsoleControl? GetLineOfSightObstruction(this ConsoleControl from, RectF to, IEnumerable<ConsoleControl> obstacles, CastingMode castingMode = CastingMode.Rough) 
        => GetLineOfSightObstruction(from, new ColliderBox(to), obstacles, castingMode);
    public static ConsoleControl? GetLineOfSightObstruction(this RectF from, RectF to, IEnumerable<ConsoleControl> obstacles, CastingMode castingMode = CastingMode.Rough) 
        => GetLineOfSightObstruction(new ColliderBox(from), new ColliderBox(to), obstacles, castingMode);
    public static HitPrediction PredictHit(ConsoleControl from, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode castingMode, HitPrediction toReuse = null, List<Edge> edgesHitOutput = null) 
        => PredictHit(from, angle, colliders, visibility, castingMode, colliders.Length, toReuse, edgesHitOutput);
    public static HitPrediction PredictHit(ConsoleControl from, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode castingMode, int bufferLen, HitPrediction toReuse = null, List<Edge> edgesHitOutput = null) 
        => PredictHit(from, CreateObstaclesFromColliders(colliders), angle, colliders, visibility, castingMode, bufferLen, toReuse, edgesHitOutput);

    public static ConsoleControl? GetLineOfSightObstruction(this ConsoleControl from, ConsoleControl to, IEnumerable<ConsoleControl> obstacleControls, CastingMode castingMode = CastingMode.Rough)
    {
        var massBounds = from.MassBounds;
        var colliders = obstacleControls.Union(new[] { to }).ToArray();
        var angle = massBounds.CalculateAngleTo(to.Bounds);
        var Visibility = 3 * massBounds.CalculateDistanceTo(to.Bounds);
        var prediction = PredictHit(from, angle, colliders, Visibility, castingMode);
        return prediction.Type == HitType.None ? null : prediction.ColliderHit == to ? null : prediction.ColliderHit;
    }

    public static HitPrediction PredictHit(ConsoleControl from, RectF[] obstacles, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode mode, int bufferLen, HitPrediction toReuse, List<Edge> edgesHitOutput = null)
    {
        var movingObject = from.MassBounds;
        var prediction = toReuse?.Clear() ?? new HitPrediction();
        prediction.LKGX = movingObject.Left;
        prediction.LKGY = movingObject.Top;
        prediction.Visibility = visibility;

        if (visibility == 0)
        {
            prediction.Type = HitType.None;
            return prediction;
        }

        visibility = visibility == float.MaxValue ? visibility : visibility + VerySmallNumber;
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

            if (from == colliders[i]) continue;

            if (from is GameCollider && colliders[i] is GameCollider)
            {
                var cc = (GameCollider)from;
                var ci = (GameCollider)colliders[i];

                if (cc.CanCollideWith(ci) == false || ci.CanCollideWith(cc) == false) continue;
            }

            if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obstacle) > visibility) continue;

            ProcessEdge(i, obstacle.TopEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.BottomEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.LeftEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.RightEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
        }

        if (closestIntersectingObstacleIndex >= 0)
        {
            prediction.ObstacleHitBounds = obstacles[closestIntersectingObstacleIndex];
            prediction.ColliderHit = colliders == null ? null : colliders[closestIntersectingObstacleIndex];
            prediction.LKGD = closestIntersectionDistance;
            prediction.LKGX = closestIntersectionX;
            prediction.LKGY = closestIntersectionY;
            prediction.Type = HitType.Obstacle;
            prediction.Edge = closestEdge;
            prediction.IntersectionX = closestIntersectionX;
            prediction.IntersectionY = closestIntersectionY;
        }

        return prediction;
    }

    private static int CreateRays(Angle angle, float visibility, CastingMode mode, RectF movingObject)
    {
        var rayCount = 0;
        rayBuffer = rayBuffer ?? new Edge[10000];
        if (mode == CastingMode.Precise)
        {
            var delta = movingObject.OffsetByAngleAndDistance(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;

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
            var delta = movingObject.OffsetByAngleAndDistance(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;

            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Top, movingObject.Left + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Top, movingObject.Right + dx, movingObject.Top + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Left, movingObject.Bottom, movingObject.Left + dx, movingObject.Bottom + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.Right, movingObject.Bottom, movingObject.Right + dx, movingObject.Bottom + dy);
            rayBuffer[rayCount++] = new Edge(movingObject.CenterX, movingObject.CenterY, movingObject.CenterX + dx, movingObject.CenterY + dy);
        }
        else if (mode == CastingMode.SingleRay)
        {
            var delta = movingObject.OffsetByAngleAndDistance(angle, visibility, normalized: false);
            var dx = delta.Left - movingObject.Left;
            var dy = delta.Top - movingObject.Top;
            rayBuffer[rayCount++] = new Edge(movingObject.CenterX, movingObject.CenterY, movingObject.CenterX + dx, movingObject.CenterY + dy);
        }
        else
        {
            throw new NotSupportedException("Unknown mode: " + mode);
        }

        return rayCount;
    }

    private static void ProcessEdge(int i, in Edge edge, int rayCount, List<Edge> edgesHitOutput, float visibility, ref float closestIntersectionDistance, ref int closestIntersectingObstacleIndex, ref Edge closestEdge, ref float closestIntersectionX, ref float closestIntersectionY)
    {
        for (var k = 0; k < rayCount; k++)
        {
            var ray = rayBuffer[k];
            if (TryFindIntersectionPoint(ray, edge, out float ix, out float iy))
            {
                edgesHitOutput?.Add(ray);
                var d = LocF.CalculateDistanceTo(ray.X1, ray.Y1, ix, iy);

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

    private static bool TryFindIntersectionPoint(in Edge a, in Edge b, out float x, out float y)
    {
        var x1 = a.X1;
        var y1 = a.Y1;
        var x2 = a.X2;
        var y2 = a.Y2;

        var x3 = b.X1;
        var y3 = b.Y1;
        var x4 = b.X2;
        var y4 = b.Y2;

        var den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (den == 0)
        {
            x = 0;
            y = 0;
            return false;
        }

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;
        if (t <= 0 || t >= 1)
        {
            x = 0;
            y = 0;
            return false;
        }

        var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / den;
        if (u > 0 && u < 1)
        {
            x = x1 + t * (x2 - x1);
            y = y1 + t * (y2 - y1);
            return true;
        }
        else
        {
            x = 0;
            y = 0;
            return false;
        }
    }

    private static RectF[] CreateObstaclesFromColliders(ConsoleControl[] colliders)
    {
        var obstacles = new RectF[colliders.Length];
        for (var i = 0; i < colliders.Length; i++)
        {
            obstacles[i] = colliders[i].Bounds;
        }
        return obstacles;
    }
}