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
        MovingObjectLeaseState?.TryDispose("external/klooie/src/klooie/Gaming/Physics/CollisionDetector.cs:42");
        MovingObjectLeaseState = null;
        ColliderHitLeaseState?.TryDispose("external/klooie/src/klooie/Gaming/Physics/CollisionDetector.cs:44");
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
            colliders.Dispose("external/klooie/src/klooie/Gaming/Physics/CollisionDetector.cs:1");
            if (autoDisposePrediction) prediction.Dispose("external/klooie/src/klooie/Gaming/Physics/CollisionDetector.cs:1");
        }
    }

    public static CollisionPrediction Predict<T>(ICollidable from, Angle angle, IList<T> colliders, float visibility, CastingMode mode, int bufferLen, CollisionPrediction prediction, List<Edge> edgesHitOutput = null) where T : ICollidable
    {
        var movingObject = from.Bounds;

        prediction.Reset();

        float aLeft = movingObject.Left;
        float aTop = movingObject.Top;
        float aRight = movingObject.Right;
        float aBottom = movingObject.Bottom;
        float aCenterX = movingObject.CenterX;
        float aCenterY = movingObject.CenterY;

        prediction.LKGX = aLeft;
        prediction.LKGY = aTop;

        if (visibility == 0f)
        {
            prediction.Visibility = 0f;
            prediction.CollisionPredicted = false;
            return prediction;
        }

        prediction.Visibility = visibility;

        if (bufferLen <= 0)
        {
            prediction.CollisionPredicted = false;
            return prediction;
        }

        var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
        float dx = delta.Left - aLeft;
        float dy = delta.Top - aTop;

        float moveLen2 = dx * dx + dy * dy;
        if (moveLen2 <= VerySmallNumberSquared)
        {
            prediction.CollisionPredicted = false;
            return prediction;
        }

        const float axisEps = 1e-8f;

        bool movesX = dx > axisEps || dx < -axisEps;
        bool movesY = dy > axisEps || dy < -axisEps;
        bool dxPositive = dx > 0f;
        bool dyPositive = dy > 0f;
        bool fromCanCollideWithEverything = CanCollideWithEverything(from);

        float invDx = movesX ? 1f / dx : 0f;
        float invDy = movesY ? 1f / dy : 0f;

        float bestT = float.PositiveInfinity;
        ICollidable bestHit = null;
        RectF bestBounds = default;
        bool bestHitVerticalFace = false;

        float sweepLeft = dxPositive ? aLeft : aLeft + dx;
        float sweepRight = dxPositive ? aRight + dx : aRight;
        float sweepTop = dyPositive ? aTop : aTop + dy;
        float sweepBottom = dyPositive ? aBottom + dy : aBottom;

        for (int i = 0; i < bufferLen; i++)
        {
            var obstacle = colliders[i];
            if (ReferenceEquals(from, obstacle)) continue;

            var obBounds = obstacle.Bounds;

            float bLeft = obBounds.Left;
            float bTop = obBounds.Top;
            float bRight = obBounds.Right;
            float bBottom = obBounds.Bottom;

            if (bRight < sweepLeft - VerySmallNumber || bLeft > sweepRight + VerySmallNumber || bBottom < sweepTop - VerySmallNumber || bTop > sweepBottom + VerySmallNumber) continue;

            if ((fromCanCollideWithEverything == false && !from.CanCollideWith(obstacle)) ||
                (CanCollideWithEverything(obstacle) == false && !obstacle.CanCollideWith(from))) continue;

            if (SweptAabbFirstHitBeatingBest(aLeft, aTop, aRight, aBottom, bLeft, bTop, bRight, bBottom, invDx, invDy, movesX, movesY, dxPositive, dyPositive, bestT, out float t, out bool hitVerticalFace))
            {
                bestT = t;
                bestHit = obstacle;
                bestBounds = obBounds;
                bestHitVerticalFace = hitVerticalFace;

                float clippedDx = dx * bestT;
                float clippedDy = dy * bestT;

                sweepLeft = clippedDx >= 0f ? aLeft : aLeft + clippedDx;
                sweepRight = clippedDx >= 0f ? aRight + clippedDx : aRight;
                sweepTop = clippedDy >= 0f ? aTop : aTop + clippedDy;
                sweepBottom = clippedDy >= 0f ? aBottom + clippedDy : aBottom;

                if (bestT <= VerySmallNumber) break;
            }
        }

        if (bestHit != null)
        {
            prediction.ObstacleHitBounds = bestBounds;
            prediction.ColliderHit = bestHit;

            float d = MathF.Sqrt(moveLen2) * bestT - 2f * VerySmallNumber;
            prediction.LKGD = d > 0f ? d : 0f;

            if (bestHitVerticalFace)
            {
                bool fromLeft = dx > 0f;
                float ix = fromLeft ? bestBounds.Left : bestBounds.Right;
                float iy = ClampFast(aCenterY + dy * bestT, bestBounds.Top, bestBounds.Bottom);

                prediction.Edge = fromLeft ? bestBounds.LeftEdge : bestBounds.RightEdge;
                prediction.LKGX = ix;
                prediction.LKGY = iy;
                prediction.IntersectionX = ix;
                prediction.IntersectionY = iy;
            }
            else
            {
                bool fromTop = dy > 0f;
                float ix = ClampFast(aCenterX + dx * bestT, bestBounds.Left, bestBounds.Right);
                float iy = fromTop ? bestBounds.Top : bestBounds.Bottom;

                prediction.Edge = fromTop ? bestBounds.TopEdge : bestBounds.BottomEdge;
                prediction.LKGX = ix;
                prediction.LKGY = iy;
                prediction.IntersectionX = ix;
                prediction.IntersectionY = iy;
            }

            prediction.CollisionPredicted = true;
        }

        return prediction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanCollideWithEverything(ICollidable collidable) => collidable is ColliderBox || collidable is RectF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SweptAabbFirstHitBeatingBest(float aLeft, float aTop, float aRight, float aBottom, float bLeft, float bTop, float bRight, float bBottom, float invDx, float invDy, bool movesX, bool movesY, bool dxPositive, bool dyPositive, float bestT, out float t, out bool hitVerticalFace)
    {
        t = 0f;
        hitVerticalFace = false;

        float xEntry;
        if (movesX)
        {
            xEntry = dxPositive ? (bLeft - aRight) * invDx : (bRight - aLeft) * invDx;
        }
        else
        {
            if (aRight <= bLeft || aLeft >= bRight) return false;
            xEntry = float.NegativeInfinity;
        }

        float yEntry;
        if (movesY)
        {
            yEntry = dyPositive ? (bTop - aBottom) * invDy : (bBottom - aTop) * invDy;
        }
        else
        {
            if (aBottom <= bTop || aTop >= bBottom) return false;
            yEntry = float.NegativeInfinity;
        }

        float tEntry = xEntry > yEntry ? xEntry : yEntry;
        if (tEntry > 1f + VerySmallNumber) return false;

        float xExit = movesX
            ? dxPositive ? (bRight - aLeft) * invDx : (bLeft - aRight) * invDx
            : float.PositiveInfinity;

        float yExit = movesY
            ? dyPositive ? (bBottom - aTop) * invDy : (bTop - aBottom) * invDy
            : float.PositiveInfinity;

        float tExit = xExit < yExit ? xExit : yExit;

        if (tEntry > tExit) return false;
        if (tExit < -VerySmallNumber) return false;

        if (tEntry < 0f) tEntry = 0f;
        else if (tEntry > 1f) tEntry = 1f;

        if (tEntry >= bestT) return false;

        t = tEntry;
        hitVerticalFace = xEntry > yEntry;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ClampFast(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
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
