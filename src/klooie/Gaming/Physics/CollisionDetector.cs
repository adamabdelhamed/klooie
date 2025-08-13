using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public class Collision : Recyclable
{
    public float MovingObjectSpeed { get; private set; }
    public Angle Angle { get; private set; }
    public ICollidable MovingObject { get; private set; }
    public ICollidable ColliderHit { get; private set; }

    public LeaseState<GameCollider> MovingObjectLeaseState { get; private set; }
    public LeaseState<GameCollider> ColliderHitLeaseState { get; private set; }

    public CollisionPrediction Prediction { get; private set; }
    public override string ToString() => $"{Prediction.LKGX},{Prediction.LKGY} - {ColliderHit?.GetType().Name}";

    protected override void OnInit() => Reset();

    public void Bind(float speed, Angle angle, ICollidable movingObject, ICollidable colliderHit, CollisionPrediction prediction)
    {
        MovingObjectSpeed = speed;
        Angle = angle;
        MovingObject = movingObject;
        ColliderHit = colliderHit;
        Prediction = prediction;

        if (movingObject is GameCollider gc) MovingObjectLeaseState = LeaseHelper.Track(gc);
        if (colliderHit is GameCollider ch) ColliderHitLeaseState = LeaseHelper.Track(ch);
    }

    public void Reset()
    {
        MovingObjectSpeed = 0;
        Angle = default;
        MovingObject = null;
        ColliderHit = null;
        Prediction = null;
        MovingObjectLeaseState?.TryDispose();
        MovingObjectLeaseState = null;
        ColliderHitLeaseState?.TryDispose();
        ColliderHitLeaseState = null;
    }
}

public sealed class CollisionPrediction : Recyclable
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

    protected override void OnInit() => Reset();

    public void Reset()
    {
        ColliderHit = null;
        ObstacleHitBounds = default;
        Edge = default;
        CollisionPredicted = false;
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
    public const float VerySmallNumber = 1e-5f;
    private const float VerySmallNumberSquared = VerySmallNumber * VerySmallNumber;

    private static Edge[] rayBuffer = null;

    public static bool HasLineOfSight<T>(this ICollidable from, ICollidable to, IList<T> obstacles)
        where T : ICollidable
        => GetLineOfSightObstruction(from, to, obstacles) == null;

    public static ICollidable? GetLineOfSightObstruction<T>(
        this ICollidable from,
        ICollidable to,
        IList<T> obstacleControls,
        CastingMode castingMode = CastingMode.Rough,
        CollisionPrediction prediction = null) where T : ICollidable
    {
        var massBounds = from.Bounds;
        var colliders = ArrayPlusOnePool<T>.Instance.Rent();
        var autoDisposePrediction = prediction == null;
        prediction ??= CollisionPredictionPool.Instance.Rent();
        colliders.Bind(obstacleControls, to);
        try
        {
            var angle = massBounds.CalculateAngleTo(to.Bounds);
            var distance = massBounds.CalculateDistanceTo(to.Bounds);
            var visibility = 3 * distance;
            Predict(from, angle, colliders, visibility, castingMode, colliders.Count, prediction);
            return prediction.CollisionPredicted == false ? null
                : prediction.ColliderHit == to ? null
                : prediction.ColliderHit;
        }
        finally
        {
            colliders.Dispose();
            if (autoDisposePrediction) prediction.Dispose();
        }
    }

    public static CollisionPrediction Predict<T>(
        ICollidable from,
        Angle angle,
        IList<T> colliders,
        float visibility,
        CastingMode mode,
        int bufferLen,
        CollisionPrediction prediction,
        List<Edge> edgesHitOutput = null) where T : ICollidable
    {
        var movingObject = from.Bounds;

        prediction.Reset();
        prediction.LKGX = movingObject.Left;
        prediction.LKGY = movingObject.Top;

        if (visibility == 0)
        {
            prediction.Visibility = 0;
            prediction.CollisionPredicted = false;
            return prediction;
        }

        prediction.Visibility = visibility;

        int rayCount = CreateRays(angle, visibility, mode, movingObject);

        float visibilitySlack = float.IsPositiveInfinity(visibility) ? visibility : (visibility + VerySmallNumber);
        float visibility2Limit = float.IsPositiveInfinity(visibilitySlack) ? float.PositiveInfinity : visibilitySlack * visibilitySlack;

        float closestIntersectionDistance2 = float.MaxValue;
        int closestIntersectingObstacleIndex = -1;
        Edge closestEdge = default;
        float closestIntersectionX = 0;
        float closestIntersectionY = 0;

        for (int i = 0; i < bufferLen; i++)
        {
            ICollidable obstacle = colliders[i];

            if (ReferenceEquals(from, obstacle) || !from.CanCollideWith(obstacle) || !obstacle.CanCollideWith(from)) continue;

            var obBounds = obstacle.Bounds;
            if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obBounds) > visibility + VerySmallNumber) continue;

            Span<Edge> singleObstacleEdgeBuffer = stackalloc Edge[4];
            singleObstacleEdgeBuffer[0] = obBounds.TopEdge;
            singleObstacleEdgeBuffer[1] = obBounds.BottomEdge;
            singleObstacleEdgeBuffer[2] = obBounds.LeftEdge;
            singleObstacleEdgeBuffer[3] = obBounds.RightEdge;

            Sort4ElementEdgeSpan(movingObject, singleObstacleEdgeBuffer);

            for (int j = 0; j < 4; j++)
            {
                var edge = singleObstacleEdgeBuffer[j];
                ProcessEdge(
                    i,
                    edge,
                    rayCount,
                    edgesHitOutput,
                    visibility2Limit,
                    ref closestIntersectionDistance2,
                    ref closestIntersectingObstacleIndex,
                    ref closestEdge,
                    ref closestIntersectionX,
                    ref closestIntersectionY);
            }
        }

        if (closestIntersectingObstacleIndex >= 0)
        {
            prediction.ObstacleHitBounds = colliders[closestIntersectingObstacleIndex].Bounds;
            prediction.ColliderHit = colliders[closestIntersectingObstacleIndex];

            // Convert once, reproduce old bias ≈ (distance - 2*eps)
            float d = MathF.Sqrt(closestIntersectionDistance2) - 2f * VerySmallNumber;
            prediction.LKGD = d > 0 ? d : 0f;

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
        rayBuffer ??= new Edge[20000];
        int rayCount = 0;

        float left = movingObject.Left, top = movingObject.Top;
        float right = movingObject.Right, bottom = movingObject.Bottom;
        float cx = movingObject.CenterX, cy = movingObject.CenterY;

        var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
        float dx = delta.Left - left;
        float dy = delta.Top - top;

        if (mode == CastingMode.Precise)
        {
            AddRay(rayBuffer, ref rayCount, new Edge(left, top, left + dx, top + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(right, top, right + dx, top + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(left, bottom, left + dx, bottom + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(right, bottom, right + dx, bottom + dy));

            const float granularity = .5f;
            float xEnd = left + movingObject.Width;
            float yEnd = top + movingObject.Height;

            for (float x = left + granularity; x < xEnd; x += granularity)
            {
                AddRay(rayBuffer, ref rayCount, new Edge(x, top, x + dx, top + dy));
                AddRay(rayBuffer, ref rayCount, new Edge(x, bottom, x + dx, bottom + dy));
            }

            for (float y = top + granularity; y < yEnd; y += granularity)
            {
                AddRay(rayBuffer, ref rayCount, new Edge(left, y, left + dx, y + dy));
                AddRay(rayBuffer, ref rayCount, new Edge(right, y, right + dx, y + dy));
            }
        }
        else if (mode == CastingMode.Rough)
        {
            AddRay(rayBuffer, ref rayCount, new Edge(left, top, left + dx, top + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(right, top, right + dx, top + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(left, bottom, left + dx, bottom + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(right, bottom, right + dx, bottom + dy));
            AddRay(rayBuffer, ref rayCount, new Edge(cx, cy, cx + dx, cy + dy));
        }
        else if (mode == CastingMode.SingleRay)
        {
            AddRay(rayBuffer, ref rayCount, new Edge(cx, cy, cx + dx, cy + dy));
        }
        else
        {
            throw new NotSupportedException("Unknown mode: " + mode);
        }

        return rayCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddRay(Edge[] buffer, ref int count, Edge ray)
    {
        if (count >= buffer.Length)
            throw new InvalidOperationException($"rayBuffer overflow: tried to add {count + 1} of {buffer.Length}");
        buffer[count++] = ray;
    }

    private static void ProcessEdge(
        int i,
        in Edge edge,
        int rayCount,
        List<Edge> edgesHitOutput,
        float visibility2Limit,  // renamed
        ref float closestIntersectionDistance2,
        ref int closestIntersectingObstacleIndex,
        ref Edge closestEdge,
        ref float closestIntersectionX,
        ref float closestIntersectionY)
    {
        for (int k = 0; k < rayCount; k++)
        {
            var ray = rayBuffer[k];
            if (TryFindIntersectionPoint(in ray, in edge, out float ix, out float iy))
            {
                edgesHitOutput?.Add(ray);

                float dx = ix - ray.X1;
                float dy = iy - ray.Y1;
                float d2 = dx * dx + dy * dy;

                if (d2 > VerySmallNumberSquared && d2 < closestIntersectionDistance2 && d2 <= visibility2Limit)
                {
                    closestIntersectionDistance2 = d2; // no epsilon shaving here
                    closestIntersectingObstacleIndex = i;
                    closestEdge = edge;
                    closestIntersectionX = ix;
                    closestIntersectionY = iy;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFindIntersectionPoint(in Edge ray, in Edge stationaryEdge, out float x, out float y)
    {
        float x1 = ray.X1, y1 = ray.Y1, x2 = ray.X2, y2 = ray.Y2;
        float x3 = stationaryEdge.X1, y3 = stationaryEdge.Y1, x4 = stationaryEdge.X2, y4 = stationaryEdge.Y2;

        // cheap AABB reject (with small tolerance)
        float eps = VerySmallNumber;
        float minRx = x1 < x2 ? x1 : x2, maxRx = x1 > x2 ? x1 : x2;
        float minRy = y1 < y2 ? y1 : y2, maxRy = y1 > y2 ? y1 : y2;
        float minSx = x3 < x4 ? x3 : x4, maxSx = x3 > x4 ? x3 : x4;
        float minSy = y3 < y4 ? y3 : y4, maxSy = y3 > y4 ? y3 : y4;

        if (maxRx + eps < minSx || maxSx + eps < minRx || maxRy + eps < minSy || maxSy + eps < minRy)
        {
            x = 0; y = 0;
            return false;
        }

        float dx1 = x2 - x1, dy1 = y2 - y1;
        float dx2 = x4 - x3, dy2 = y4 - y3;

        float den = dx1 * dy2 - dy1 * dx2;

        if (MathF.Abs(den) < 1e-8f)
        {
            float det = (x1 - x3) * (y2 - y3) - (y1 - y3) * (x2 - x3);
            if (MathF.Abs(det) >= 1e-8f)
            {
                x = 0; y = 0;
                return false;
            }

            if (Math.Max(x1, x2) < Math.Min(x3, x4) ||
                Math.Max(x3, x4) < Math.Min(x1, x2) ||
                Math.Max(y1, y2) < Math.Min(y3, y4) ||
                Math.Max(y3, y4) < Math.Min(y1, y2))
            {
                x = 0; y = 0;
                return false;
            }

            x = x3; y = y3;
            return true;
        }

        // compute t first; bail early if outside [0,1] to save the second division sometimes
        float t = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / den;
        if (t < -1e-5f || t > 1 + 1e-5f)
        {
            x = 0; y = 0;
            return false;
        }

        float u = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / den;
        if (u < -1e-5f || u > 1 + 1e-5f)
        {
            x = 0; y = 0;
            return false;
        }

        x = x1 + t * dx1;
        y = y1 + t * dy1;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Sort4ElementEdgeSpan(RectF rect, Span<Edge> edges)
    {
        float cx = rect.CenterX, cy = rect.CenterY;

        Edge e0 = edges[0], e1 = edges[1], e2 = edges[2], e3 = edges[3];
        float d0 = e0.CalculateDistanceTo(cx, cy);
        float d1 = e1.CalculateDistanceTo(cx, cy);
        float d2 = e2.CalculateDistanceTo(cx, cy);
        float d3 = e3.CalculateDistanceTo(cx, cy);

        if (d0 > d1) { (d0, d1) = (d1, d0); (e0, e1) = (e1, e0); }
        if (d1 > d2) { (d1, d2) = (d2, d1); (e1, e2) = (e2, e1); }
        if (d2 > d3) { (d2, d3) = (d3, d2); (e2, e3) = (e3, e2); }
        if (d0 > d1) { (d0, d1) = (d1, d0); (e0, e1) = (e1, e0); }
        if (d1 > d2) { (d1, d2) = (d2, d1); (e1, e2) = (e2, e1); }
        if (d0 > d1) { (d0, d1) = (d1, d0); (e0, e1) = (e1, e0); }

        edges[0] = e0; edges[1] = e1; edges[2] = e2; edges[3] = e3;
    }
}

public class ArrayPlusOnePool<T> : RecycleablePool<ArrayPlusOne<T>> where T : ICollidable
{
    private static ArrayPlusOnePool<T> instance;
    public static ArrayPlusOnePool<T> Instance => instance ??= new ArrayPlusOnePool<T>();

    public override ArrayPlusOne<T> Factory() => new ArrayPlusOne<T>();
}

public class ArrayPlusOne<T> : Recyclable, IList<ICollidable> where T : ICollidable
{
    public int Length => hasExtra ? Array.Count + 1 : Array.Count;
    public int Count => Length;
    public bool IsReadOnly => true;

    private bool hasExtra;
    private IList<T> Array;
    private ICollidable ExtraElement;

    public ICollidable this[int index]
    {
        get => (index == Array.Count) ? ExtraElement : Array[index];
        set => throw new NotSupportedException();
    }

    protected override void OnInit()
    {
        base.OnInit();
        Array = null;
        ExtraElement = default;
        hasExtra = false;
    }

    public void Bind(IList<T> array)
    {
        Array = array;
        hasExtra = false;
    }

    public void Bind(IList<T> array, ICollidable extraElement)
    {
        Array = array;
        ExtraElement = extraElement;
        hasExtra = true;
    }

    public int IndexOf(ICollidable item) => throw new NotSupportedException();
    public void Insert(int index, ICollidable item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void Add(ICollidable item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(ICollidable item) => throw new NotSupportedException();
    public void CopyTo(ICollidable[] array, int arrayIndex) => throw new NotSupportedException();
    public bool Remove(ICollidable item) => throw new NotSupportedException();
    public IEnumerator<ICollidable> GetEnumerator() => throw new NotSupportedException();
    IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
}
