﻿//using System.Drawing;
//using System.Drawing.Imaging;

namespace klooie.Gaming;

public struct Collision
{
    public float MovingObjectSpeed { get; set; }
    public Angle Angle { get; set; }
    public ConsoleControl MovingObject { get; set; }
    public ConsoleControl ColliderHit { get; set; }
    public CollisionPrediction Prediction { get; set; }
    public override string ToString() => $"{Prediction.LKGX},{Prediction.LKGY} - {ColliderHit?.GetType().Name}";
}

public sealed class CollisionPrediction
{
    public bool CollisionPredicted { get; set; }
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

    [ThreadStatic]
    private static Edge[] rayBuffer;

    public static bool HasLineOfSight(this Velocity from, ConsoleControl to)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            from.GetObstacles(buffer);
            return HasLineOfSight(from.Collider, to, buffer.ReadableBuffer);
        }
        finally
        {
            ObstacleBufferPool.Instance.Return(buffer);
        }
    }
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
    public static CollisionPrediction Predict(ConsoleControl from, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode castingMode, CollisionPrediction toReuse = null, List<Edge> edgesHitOutput = null) 
        => Predict(from, angle, colliders, visibility, castingMode, colliders.Length, toReuse, edgesHitOutput);
    public static CollisionPrediction Predict(ConsoleControl from, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode castingMode, int bufferLen, CollisionPrediction toReuse = null, List<Edge> edgesHitOutput = null) 
        => Predict(from, CreateObstaclesFromColliders(colliders), angle, colliders, visibility, castingMode, bufferLen, toReuse, edgesHitOutput);

    public static ConsoleControl? GetLineOfSightObstruction(this ConsoleControl from, ConsoleControl to, IEnumerable<ConsoleControl> obstacleControls, CastingMode castingMode = CastingMode.Rough)
    {
        var massBounds = from.Bounds;
        var colliders = obstacleControls.Union(new[] { to }).ToArray();
        var angle = massBounds.CalculateAngleTo(to.Bounds);
        var Visibility = 3 * massBounds.CalculateDistanceTo(to.Bounds);
        var prediction = Predict(from, angle, colliders, Visibility, castingMode);
        return prediction.CollisionPredicted == false ? null : prediction.ColliderHit == to ? null : prediction.ColliderHit;
    }

    public static CollisionPrediction Predict(ConsoleControl from, RectF[] obstacles, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode mode, int bufferLen, CollisionPrediction toReuse, List<Edge> edgesHitOutput = null)
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

            if (from == colliders[i]) continue;
            if (colliders[i].ShouldStop) continue;

            if (from is GameCollider && colliders[i] is GameCollider)
            {
                var cc = (GameCollider)from;
                var ci = (GameCollider)colliders[i];

                if (cc.CanCollideWith(ci) == false || ci.CanCollideWith(cc) == false) continue;
            }

            if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obstacle) > visibility + VerySmallNumber) continue;

            ProcessEdge(i, obstacle.TopEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.BottomEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.LeftEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            ProcessEdge(i, obstacle.RightEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);

            //  if(obstacle.NumberOfPixelsThatOverlap(movingObject) > 0 && closestIntersectingObstacleIndex < 0)
            //  {
            //throw new Exception("object is touching, but intersection not found");
            //  }
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

       //     if(prediction.LKGD > visibility)
       //     {
       //         throw new Exception($"LKGD of {prediction.LKGD} is > visibility {visibility}");
       //     }

        }

        return prediction;
    }
    /*
    public static Bitmap DebugPrediction(ConsoleControl from, RectF[] obstacles, Angle angle, ConsoleControl[] colliders, float visibility, CastingMode mode, int bufferLen, CollisionPrediction toReuse, List<Edge> edgesHitOutput = null)
    {
        var xMultiplier = 10;
        var yMultiplier = 20;


        using (var ret = new Bitmap(Game.Current.GamePanel.Width * xMultiplier, Game.Current.GamePanel.Height * yMultiplier))
        using(var graphics = Graphics.FromImage(ret))
        {
            Func<float,float> adjustX = (x) => x * xMultiplier + (ret.Width / 2f);
            Func<float, float> adjustY = (y) => y * yMultiplier + (ret.Height / 2f);
            Func<RectF, RectF> adjustRect = (r) => new RectF(adjustX(r.Left), adjustY(r.Top), r.Width * xMultiplier, r.Height * yMultiplier);
            Func<Edge, Edge> adjustEdge = (e) => new Edge(adjustX(e.X1), adjustY(e.Y1), adjustX(e.X2), adjustY(e.Y2));
            Func<LocF, LocF> adjustLocF = (l) => new LocF(adjustX(l.Left), adjustY(l.Top));
            graphics.FillRectangle(Brushes.Black, 0, 0, ret.Width, ret.Height);
            obstacles.Select(adjustRect).ForEach(adjusted => graphics.FillRectangle(Brushes.White, adjusted.Left, adjusted.Top, adjusted.Width, adjusted.Height));
          
            var movingObject = from.Bounds;
            var prediction = toReuse?.Clear() ?? new CollisionPrediction();
            prediction.LKGX = movingObject.Left;
            prediction.LKGY = movingObject.Top;
            prediction.Visibility = visibility;

            var movingObjectAdjusted = adjustRect(movingObject);
            graphics.FillRectangle(Brushes.Green, movingObjectAdjusted.Left, movingObjectAdjusted.Top, movingObjectAdjusted.Width, movingObjectAdjusted.Height);

            if (visibility == 0)
            {
                prediction.CollisionPredicted = false;
                ret.Save(@"C:\temp\debug.png", ImageFormat.Png);
                return ret;
            }

            prediction.Visibility = visibility;

            var rayCount = CreateRays(angle, visibility, mode, movingObject);

            var closestIntersectionDistance = float.MaxValue;
            int closestIntersectingObstacleIndex = -1;
            Edge closestEdge = default;
            float closestIntersectionX = 0;
            float closestIntersectionY = 0;
            var len = bufferLen;
            Edge closestRay = default;
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

                if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obstacle) > visibility + VerySmallNumber) continue;

                ProcessEdge(i, obstacle.TopEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY, ref closestRay);
                ProcessEdge(i, obstacle.BottomEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY, ref closestRay);
                ProcessEdge(i, obstacle.LeftEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY, ref closestRay);
                ProcessEdge(i, obstacle.RightEdge, rayCount, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY, ref closestRay);

                //  if(obstacle.NumberOfPixelsThatOverlap(movingObject) > 0 && closestIntersectingObstacleIndex < 0)
                //  {
                //throw new Exception("object is touching, but intersection not found");
                //  }
            }

            if (closestIntersectingObstacleIndex >= 0)
            {
                
                var closestEdgeAdjusted = adjustEdge(closestEdge);
                var closestIntersectingRayAdjusted = adjustEdge(closestRay);

                var movingBoundsAdjusted = adjustRect(movingObject);
                graphics.DrawRectangle(Pens.Magenta, movingBoundsAdjusted.Left, movingBoundsAdjusted.Top, movingBoundsAdjusted.Width, movingBoundsAdjusted.Height);
                graphics.DrawLine(Pens.Magenta, closestIntersectingRayAdjusted.X1, closestIntersectingRayAdjusted.Y1, adjustX(closestIntersectionX), adjustY(closestIntersectionY));
                graphics.DrawLine(Pens.Red, closestEdgeAdjusted.X1, closestEdgeAdjusted.Y1, closestEdgeAdjusted.X2, closestEdgeAdjusted.Y2);

                prediction.ObstacleHitBounds = obstacles[closestIntersectingObstacleIndex];
                prediction.ColliderHit = colliders == null ? null : colliders[closestIntersectingObstacleIndex];
                prediction.LKGD = closestIntersectionDistance;
                prediction.LKGX = closestIntersectionX;
                prediction.LKGY = closestIntersectionY;
                prediction.CollisionPredicted = true;
                prediction.Edge = closestEdge;
                prediction.IntersectionX = closestIntersectionX;
                prediction.IntersectionY = closestIntersectionY;

                //     if(prediction.LKGD > visibility)
                //     {
                //         throw new Exception($"LKGD of {prediction.LKGD} is > visibility {visibility}");
                //     }

            }
            ret.Save(@"C:\temp\debug.png", ImageFormat.Png);
            return ret;
        }
    }
    */

    private static int CreateRays(Angle angle, float visibility, CastingMode mode, RectF movingObject)
    {
        var rayCount = 0;
        rayBuffer = rayBuffer ?? new Edge[10000];
        if (mode == CastingMode.Precise)
        {
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
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
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
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
            var delta = movingObject.RadialOffset(angle, visibility, normalized: false);
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
                var d = LocF.CalculateDistanceTo(ray.X1, ray.Y1, ix, iy) - VerySmallNumber;

          //      if (d > visibility)
          //      {
          //          throw new Exception($"intersection distance of {d} is > visibility of {visibility}");
          //      }

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
  
    // Old code that had lots of strange handling of edge cases 
    /*

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
                // There is a special case where den == 0, and yet there is an intersection.
                //
                // The case is when the two edges are parallel with each other. In that
                // case we need to do a little more checking before we know if they 
                // intersect.


                // First we see if the slopes are different. If they are then they are not
                // parallel. This means they do not fall into the special case and the 2 edges
                // do not intersect.
                var raySlope = ray.From.CalculateAngleTo(ray.To);
                var stationaryEdgeSlope = stationaryEdge.From.CalculateAngleTo(stationaryEdge.To);
                if (raySlope != stationaryEdgeSlope)
                {
                    x = 0;
                    y = 0;
                    return false;
                }

                // if these parallel lines share and endpoint then the intersection is that endpoint
                if (ray.X1 == stationaryEdge.X1 && ray.Y1 == stationaryEdge.Y1)
                {
                    x = ray.X1;
                    y = ray.Y1;
                    return true;
                }

                if (ray.X1 == stationaryEdge.X2 && ray.Y1 == stationaryEdge.Y2)
                {
                    x = ray.X1;
                    y = ray.Y1;
                    return true;
                }

                // The slopes are the same so we need to perform the final test.
                // We will create 4 new edges, two for the ray and 2 for the stationary edge.
                // They will be perpendicular to the edge they were created from and they will be
                // centered on the point they were created from.
                //
                // For the 2 edges created from the ray, test to see if they intersect with the stationary edge.
                // For the 2 edges created from the stationary edge, test to see if they intersect with the ray.
                // That is a total of 4 tests.

                var up = ray.From.RadialOffset(raySlope.Add(-90), 1, false);
                var down = ray.From.RadialOffset(raySlope.Add(90), 1, false);
                var rayPerp1 = new Edge(up.Left, up.Top, down.Left, down.Top);

                up = ray.To.RadialOffset(raySlope.Add(-90), 1, false);
                down = ray.To.RadialOffset(raySlope.Add(90), 1, false);
                var rayPerp2 = new Edge(up.Left, up.Top, down.Left, down.Top);

                up = stationaryEdge.From.RadialOffset(stationaryEdgeSlope.Add(-90), 1, false);
                down = stationaryEdge.From.RadialOffset(stationaryEdgeSlope.Add(90), 1, false);
                var statPerp1 = new Edge(up.Left, up.Top, down.Left, down.Top);

                up = stationaryEdge.To.RadialOffset(stationaryEdgeSlope.Add(-90), 1, false);
                down = stationaryEdge.To.RadialOffset(stationaryEdgeSlope.Add(90), 1, false);
                var statPerp2 = new Edge(up.Left, up.Top, down.Left, down.Top);

                var test1 = TryFindIntersectionPoint(rayPerp1, stationaryEdge, out float test1X, out float test1Y);
                var test2 = TryFindIntersectionPoint(rayPerp2, stationaryEdge, out float test2X, out float test2Y);
                var test3 = TryFindIntersectionPoint(statPerp1, ray, out float test3X, out float test3Y);
                var test4 = TryFindIntersectionPoint(statPerp2, ray, out float test4X, out float test4Y);

                // If none of these tests produce an intersection then we can return false.
                if (test1 == false && test2 == false && test3 == false && test4 == false)
                {
                    x = 0;
                    y = 0;
                    return false;
                }

                // There is an intersection. Our final challenge is to determine where the intersection happens.
                //         
                // This is not easy since overlapping, parallel line segments can intersect at an infinite number
                // of points. But we want to choose the point where the ray meets the stationary object in the ray
                // direction. Do do this, we'll look at the subet of our 4 tests where an intersection was found.
                // For each one we'll create an edge starting from the ray's starting point and ending at the 
                // intersection. We will return the shortest edge and report that as the intersection point.

                var edgeBuffer = new Edge[4];
                var edgeIndex = 0;
                if (test1) edgeBuffer[edgeIndex++] = new Edge(ray.X1, ray.Y1, test1X, test1Y);
                if (test2) edgeBuffer[edgeIndex++] = new Edge(ray.X1, ray.Y1, test2X, test2Y);
                if (test3) edgeBuffer[edgeIndex++] = new Edge(ray.X1, ray.Y1, test3X, test3Y);
                if (test4) edgeBuffer[edgeIndex++] = new Edge(ray.X1, ray.Y1, test4X, test4Y);

                var shortestD = float.MaxValue;
                var shortestEdge = default(Edge);
                for (var i = 0; i < edgeIndex; i++)
                {
                    var finalTestSlope = edgeBuffer[i].From.CalculateAngleTo(edgeBuffer[i].To);

                    // if the lines had the same slope, but were separated by a very small margin then the slope
                    // will be different so we can count it out
                    if (finalTestSlope != raySlope) continue;

                    // if this candidate intersection point doesn't acutally lie
                    // on both the ray edge and the stationary edge then there is no
                    // intersection
                    var isOnRay = ray.Contains(edgeBuffer[i].To);
                    if (isOnRay == false) continue;
                    var isOnEdge = stationaryEdge.Contains(edgeBuffer[i].To);
                    if (isOnEdge == false) continue;

                    var d = edgeBuffer[i].From.CalculateDistanceTo(edgeBuffer[i].To);
                    if (d < shortestD)
                    {
                        shortestD = d;
                        shortestEdge = edgeBuffer[i];
                    }
                }


                if (shortestD < float.MaxValue)
                {
                    x = shortestEdge.X2;
                    y = shortestEdge.Y2;
                    return true;
                }
                else
                {
                    x = 0;
                    y = 0;
                    return false;
                }
            }

            var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den;
            if (t < 0 || t > 1)
            {
                x = 0;
                y = 0;
                return false;
            }

            var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / den;
            if (u >= 0 && u <= 1)
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
    */

    // new implementation from chatGPT (GPT4) - much cleaner, tests passing, and chatGPT even added a few more tests
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
            if (Math.Max(x1, x2) < Math.Min(x3, x4) || Math.Max(x3, x4) < Math.Min(x1, x2) ||
                Math.Max(y1, y2) < Math.Min(y3, y4) || Math.Max(y3, y4) < Math.Min(y1, y2))
            {
                x = 0;
                y = 0;
                return false;
            }

            // Find the intersection point as the point where the two segments start overlapping
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

        // epsilon check for floating point errors
        bool between(float a, float b, float c, float epsilon = 1e-5f)
        {
            return a - epsilon <= b && b <= c + epsilon || c - epsilon <= b && b <= a + epsilon;
        }

        if (between(x1, x, x2) && between(y1, y, y2) && between(x3, x, x4) && between(y3, y, y4))
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