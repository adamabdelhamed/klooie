﻿using System.Collections;

namespace klooie.Gaming;
public class Collision : Recyclable
{
    public float MovingObjectSpeed { get; private set; }
    public Angle Angle { get; private set; }
    public ICollidable MovingObject { get; private set; }
    public ICollidable ColliderHit { get; private set; }
    public CollisionPrediction Prediction { get; private set; }
    public override string ToString() => $"{Prediction.LKGX},{Prediction.LKGY} - {ColliderHit?.GetType().Name}";

    protected override void OnInit()
    {
        Reset();
    }

    public void Bind(float speed, Angle angle, ICollidable movingObject, ICollidable colliderHit, CollisionPrediction prediction)
    {
        MovingObjectSpeed = speed;
        Angle = angle;
        MovingObject = movingObject;
        ColliderHit = colliderHit;
        Prediction = prediction;
    }

    public void Reset()
    {
        MovingObjectSpeed = 0;
        Angle = default;
        MovingObject = null;
        ColliderHit = null;
        Prediction = null;
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


    protected override void OnInit()
    {
        Reset();
    }

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

    private static Edge[] rayBuffer = null;
    private static Edge[] singleObstacleEdgeBuffer = new Edge[4];
    public static bool HasLineOfSight<T>(this ICollidable from, ICollidable to, IList<T> obstacles) where T : ICollidable => GetLineOfSightObstruction(from, to, obstacles) == null;

    public static ICollidable? GetLineOfSightObstruction<T>( this ICollidable from, ICollidable to, IList<T> obstacleControls, CastingMode castingMode = CastingMode.Rough) where T : ICollidable
    {
        var massBounds = from.Bounds;
        var colliders = ArrayPlusOnePool<T>.Instance.Rent();
        var prediction = CollisionPredictionPool.Instance.Rent();
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
            prediction.Dispose();
        }
    }
 
    public static CollisionPrediction Predict<T>(ICollidable from,Angle angle,IList<T> colliders, float visibility,CastingMode mode,int bufferLen,CollisionPrediction prediction,List<Edge> edgesHitOutput = null) where T : ICollidable
    {
        var movingObject = from.Bounds;
        prediction.Reset();
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
   
        for (var i = 0; i < bufferLen; i++)
        {
            ICollidable obstacle = colliders[i];

            if (ReferenceEquals(from, obstacle) || !from.CanCollideWith(obstacle) || !obstacle.CanCollideWith(from)) continue;
            if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obstacle.Bounds) > visibility + VerySmallNumber) continue;

            singleObstacleEdgeBuffer[0] = obstacle.Bounds.TopEdge;
            singleObstacleEdgeBuffer[1] = obstacle.Bounds.BottomEdge;
            singleObstacleEdgeBuffer[2] = obstacle.Bounds.LeftEdge;
            singleObstacleEdgeBuffer[3] = obstacle.Bounds.RightEdge;

            CustomSorter.Sort(movingObject, singleObstacleEdgeBuffer);
           
            for(var j = 0; j < singleObstacleEdgeBuffer.Length; j++)
            {
                var edge = singleObstacleEdgeBuffer[j];
                ProcessEdge(i, edge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            }
        }

        if (closestIntersectingObstacleIndex >= 0)
        {
            prediction.ObstacleHitBounds = colliders[closestIntersectingObstacleIndex].Bounds;
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
        rayBuffer = rayBuffer ?? new Edge[20000];
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

                if (d > VerySmallNumber && d < closestIntersectionDistance && d <= visibility)
                {
                    closestIntersectionDistance = d - VerySmallNumber;
                    closestIntersectingObstacleIndex = i;
                    closestEdge = edge;
                    closestIntersectionX = ix;
                    closestIntersectionY = iy;
                }
            }
        }
    }

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

        if (Between(x1, x, x2) && Between(y1, y, y2) && Between(x3, x, x4) && Between(y3, y, y4))
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

    private static bool Between(float a, float b, float c, float eps = 1e-5f) => (a - eps <= b && b <= c + eps) || (c - eps <= b && b <= a + eps);

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

    // indexer for get
    public ICollidable this[int index]
    {
        get
        {
            if (index == Array.Count)
            {
                return ExtraElement;
            }
            return Array[index];
        }
        set
        {
            throw new NotSupportedException();
        }
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

    public int IndexOf(ICollidable item)
    {
        throw new NotImplementedException();
    }

    public void Insert(int index, ICollidable item)
    {
        throw new NotImplementedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    public void Add(ICollidable item)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(ICollidable item)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(ICollidable[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public bool Remove(ICollidable item)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<ICollidable> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}

public static class CustomSorter
{
    private static float centerX;
    private static float centerY;

    public static void Sort(RectF movingObjectBounds, Edge[] obstacleEdgeBuffer)
    {
        // Set static variables to avoid capturing or passing them
        centerX = movingObjectBounds.Center.Left;
        centerY = movingObjectBounds.Center.Top;

        // Use a static comparison method
        Array.Sort(obstacleEdgeBuffer, CompareEdges);
    }

    private static int CompareEdges(Edge edge1, Edge edge2)
    {
        var distance1 = edge1.CalculateDistanceTo(centerX, centerY);
        var distance2 = edge2.CalculateDistanceTo(centerX, centerY);
        return distance1.CompareTo(distance2);
    }
}