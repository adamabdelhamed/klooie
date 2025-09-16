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

    public Collision Bind(float speed, Angle angle, ICollidable movingObject, ICollidable colliderHit, CollisionPrediction prediction)
    {
        MovingObjectSpeed = speed;
        Angle = angle;
        MovingObject = movingObject;
        ColliderHit = colliderHit;
        Prediction = prediction;

        if (movingObject is GameCollider gc) MovingObjectLeaseState = LeaseHelper.Track(gc);
        if (colliderHit is GameCollider ch) ColliderHitLeaseState = LeaseHelper.Track(ch);
        return this;
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
        // Preserve existing init/reset behavior & LKG defaults
        var movingObject = from.Bounds;

        prediction.Reset();
        prediction.LKGX = movingObject.Left;
        prediction.LKGY = movingObject.Top;

        if (visibility == 0f)
        {
            prediction.Visibility = 0f;
            prediction.CollisionPredicted = false;
            return prediction;
        }
        prediction.Visibility = visibility;

        // Compute movement vector exactly like CreateRays()
        var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
        float dx = delta.Left - movingObject.Left;
        float dy = delta.Top - movingObject.Top;

        // If effectively stationary, bail
        float moveLen2 = dx * dx + dy * dy;
        if (moveLen2 <= VerySmallNumberSquared)
        {
            prediction.CollisionPredicted = false;
            return prediction;
        }

        // Tracks best (closest) hit
        float bestT = float.PositiveInfinity;      // normalized 0..1 along the cast
        int bestIndex = -1;
        Edge bestEdge = default;
        float bestIX = 0f, bestIY = 0f;

        // Visibility check (same semantics as before)
        float visibilitySlack = float.IsPositiveInfinity(visibility) ? visibility : (visibility + VerySmallNumber);
        float visibility2Limit = float.IsPositiveInfinity(visibilitySlack) ? float.PositiveInfinity : visibilitySlack * visibilitySlack;

        for (int i = 0; i < bufferLen; i++)
        {
            var obstacle = colliders[i];
            if (ReferenceEquals(from, obstacle) || !from.CanCollideWith(obstacle) || !obstacle.CanCollideWith(from)) continue;

            var obBounds = obstacle.Bounds;

            // Same coarse distance reject you already had
            if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obBounds) > visibility + VerySmallNumber) continue;

            // O(1) swept AABB test; returns earliest TOI t in [0, 1] if hit
            if (SweptAabbFirstHit(movingObject, obBounds, dx, dy, out float t, out Edge hitEdge, out float ix, out float iy))
            {
                // Convert to distance^2 along the path to keep old compare semantics
                // distance = |v| * t  (|v| ~= visibility)
                float dist2 = moveLen2 * (t * t);

                // Respect visibility^2 ceiling (kept for parity with previous behavior)
                if (dist2 <= visibility2Limit && t >= -1e-6f && t <= 1f + 1e-6f)
                {
                    if (t < bestT)
                    {
                        bestT = t;
                        bestIndex = i;
                        bestEdge = hitEdge;
                        bestIX = ix;
                        bestIY = iy;

                        // Optional: short-circuit if we literally hit at t==0
                        if (bestT <= VerySmallNumber) break;
                    }
                }
            }
        }

        if (bestIndex >= 0)
        {
            var hit = colliders[bestIndex];
            prediction.ObstacleHitBounds = hit.Bounds;
            prediction.ColliderHit = hit;

            // Distance along path minus the same 2*eps bias shave you already applied
            float d = MathF.Sqrt(moveLen2) * bestT - 2f * VerySmallNumber;
            prediction.LKGD = d > 0f ? d : 0f;

            prediction.LKGX = bestIX;
            prediction.LKGY = bestIY;
            prediction.CollisionPredicted = true;
            prediction.Edge = bestEdge;
            prediction.IntersectionX = bestIX;
            prediction.IntersectionY = bestIY;

            // No rays in the new algorithm; edgesHitOutput intentionally unused.
        }

        return prediction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SweptAabbFirstHit(
        in RectF a,          // moving rect (start pose)
        in RectF b,          // stationary rect
        float dx, float dy,  // movement vector over the "visibility" path
        out float t,         // normalized time of impact in [0,1]
        out Edge hitEdge,    // which physical edge on 'b' we touched
        out float ix, out float iy) // contact point
    {
        // Defaults
        t = 0f; ix = 0f; iy = 0f; hitEdge = default;
        const float eps = 1e-8f;

        // Compute entry/exit times for X
        float xEntry, xExit;
        if (MathF.Abs(dx) <= eps)
        {
            // No horizontal motion: must *already* overlap in X to collide
            if (a.Right <= b.Left || a.Left >= b.Right) return false; // <-- no epsilon here
            xEntry = float.NegativeInfinity;
            xExit = float.PositiveInfinity;
        }
        else if (dx > 0f)
        {
            xEntry = (b.Left - a.Right) / dx;
            xExit = (b.Right - a.Left) / dx;
        }
        else
        {
            xEntry = (b.Right - a.Left) / dx; // dx < 0
            xExit = (b.Left - a.Right) / dx;
        }

        // Y axis
        float yEntry, yExit;
        if (MathF.Abs(dy) <= eps)
        {
            if (a.Bottom <= b.Top || a.Top >= b.Bottom) return false; // <-- no epsilon here
            yEntry = float.NegativeInfinity;
            yExit = float.PositiveInfinity;
        }
        else if (dy > 0f)
        {
            yEntry = (b.Top - a.Bottom) / dy;
            yExit = (b.Bottom - a.Top) / dy;
        }
        else
        {
            yEntry = (b.Bottom - a.Top) / dy; // dy < 0
            yExit = (b.Top - a.Bottom) / dy;
        }

        float tEntry = MathF.Max(xEntry, yEntry);
        float tExit = MathF.Min(xExit, yExit);

        // Bail if no overlap during [0,1]
        if (tEntry > tExit) return false;
        if (tExit < -VerySmallNumber) return false;
        if (tEntry > 1f + VerySmallNumber) return false;

        // Clamp for grazing forgiveness
        if (tEntry < 0f) tEntry = 0f;
        if (tEntry > 1f) tEntry = 1f;
        t = tEntry;

        // Pick edge
        bool hitVerticalFace = xEntry > yEntry;
        if (hitVerticalFace)
        {
            bool fromLeft = dx > 0f;
            hitEdge = fromLeft ? b.LeftEdge : b.RightEdge;
            ix = fromLeft ? b.Left : b.Right;
            float aCy = a.CenterY + dy * t;
            iy = Math.Clamp(aCy, b.Top, b.Bottom);
        }
        else
        {
            bool fromTop = dy > 0f;
            hitEdge = fromTop ? b.TopEdge : b.BottomEdge;
            iy = fromTop ? b.Top : b.Bottom;
            float aCx = a.CenterX + dx * t;
            ix = Math.Clamp(aCx, b.Left, b.Right);
        }

        return true;
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
