﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PowerArgs.Cli.Physics
{
    public enum HitType
    {
        None = 0,
        Obstacle = 1,
    }

    public struct Impact
    {
        public Angle Angle { get; set; }
        public ICollider MovingObject { get; set; }
        public ICollider ColliderHit { get; set; }
        public HitType HitType { get; set; }
        public HitPrediction Prediction { get; set; }
    }

    public class HitPrediction
    {
        public HitType Type { get; set; }
        public RectF ObstacleHitBounds { get; set; }
        public ICollider ColliderHit { get; set; }
        public float LKGX { get; set; }
        public float LKGY { get; set; }
        public float LKGD { get; set; }
        public float Visibility { get; set; }
        public bool ElementWasAlreadyObstructed { get; set; }

        public Edge Edge { get; set; }

        public float IntersectionX { get; set; }
        public float IntersectionY { get; set; }

        public LocF Intersection => new LocF(IntersectionX, IntersectionY);

        public void Clear()
        {
            ColliderHit = null;
            ObstacleHitBounds = default;
            Edge = default;
            Type = HitType.None;
        }
    }

    public class HitDetectionOptions
    {
        public RectF MovingObject { get; set; }
        public RectF[] Obstacles { get; set; }

        internal ICollider[] Colliders { get; set; }

        public Angle Angle { get; set; }
        public float Visibility { get; set; } 

        public int? ObstacleBufferLength { get; set; }

        public CastingMode Mode { get; set; } = CastingMode.Precise;

        public List<Edge> EdgesHitOutput { get; set; }

        public HitDetectionOptions()
        {

        }

        public HitDetectionOptions(ICollider c, IEnumerable<ICollider> obstacles)
        {
            MovingObject = c.Bounds;
            Colliders = obstacles.ToArray();
            Obstacles = new RectF[Colliders.Length];
            for (var i = 0; i < Colliders.Length; i++)
            {
                Obstacles[i] = Colliders[i].Bounds;
            }
        }

        public HitDetectionOptions(ICollider c, ICollider[] colliders, RectF[] obstacles, int length)
        {
            MovingObject = c.Bounds;
            Colliders = colliders;
            Obstacles = obstacles;
            ObstacleBufferLength = length;
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
        public static bool HasLineOfSight(this Velocity2 from, ICollider to) => HasLineOfSight(from.Collider, to, from.GetObstaclesSlow());
 
        public static bool HasLineOfSight(this ICollider from, ICollider to, IEnumerable<ICollider> obstacles) => GetLineOfSightObstruction(from, to, obstacles) == null;
        public static bool HasLineOfSight(this ICollider from, RectF to, IEnumerable<ICollider> obstacles) => GetLineOfSightObstruction(from, to, obstacles) == null;
        public static bool HasLineOfSight(this RectF from, ICollider to, IEnumerable<ICollider> obstacles) => GetLineOfSightObstruction(from, to, obstacles) == null;
        public static bool HasLineOfSight(this RectF from, RectF to, IEnumerable<ICollider> obstacles) => GetLineOfSightObstruction(from, to, obstacles) == null;
        public static bool HasLineOfSight(this RectF from, RectF to, IEnumerable<RectF> obstacles) => GetLineOfSightObstruction(from, to, obstacles.Select(o => new ColliderBox(o))) == null;
        public static ICollider GetLineOfSightObstruction(this ICollider from, ICollider to, IEnumerable<ICollider> obstacles, CastingMode castingMode = CastingMode.Rough)
        {
            var options = new HitDetectionOptions(from, obstacles.Union(new[] { to }));
            options.Mode = castingMode;
            options.Angle = options.MovingObject.CalculateAngleTo(to.Bounds);
            options.Visibility = 3 * options.MovingObject.CalculateDistanceTo(to.Bounds);

            var prediction = PredictHit(options);

            if (prediction.Type == HitType.None)
            {
                return null;
            }
            else
            {
                var obstacleHit = prediction.ColliderHit;
                return obstacleHit == to ? null : obstacleHit;
            }
        }

        public static ICollider GetLineOfSightObstruction(this RectF from, ICollider to, IEnumerable<ICollider> obstacles, CastingMode castingMode = CastingMode.Rough)
        {
            var fromBox = new ColliderBox(from);
            return GetLineOfSightObstruction(fromBox, to, obstacles, castingMode);
        }

        public static ICollider GetLineOfSightObstruction(this ICollider from, RectF to, IEnumerable<ICollider> obstacles, CastingMode castingMode = CastingMode.Rough)
        {
            var toBox = new ColliderBox(to);
            return GetLineOfSightObstruction(from, toBox, obstacles, castingMode);
        }

        public static ICollider GetLineOfSightObstruction(this RectF from, RectF to, IEnumerable<ICollider> obstacles, CastingMode castingMode = CastingMode.Rough)
        {
            var fromBox = new ColliderBox(from);
            var toBox = new ColliderBox(to);
            return GetLineOfSightObstruction(fromBox, toBox, obstacles, castingMode);
        }

        [ThreadStatic]
        private static Edge[] castBuffer;

        public static HitPrediction PredictHit(HitDetectionOptions options)
        {
            return PredictHit(options.MovingObject, options.Obstacles.ToArray(), options.Angle, options.Colliders, options.Visibility, options.Mode, options.EdgesHitOutput, options.ObstacleBufferLength);
        }

        public static HitPrediction PredictHit(in RectF movingObject, RectF[] obstacles, Angle angle, ICollider[] colliders = null, float visibility = 10000f, CastingMode mode = CastingMode.Precise, List<Edge> edgesHitOutput = null, int? bufferLen = null, HitPrediction toReuse = null)
        {
            var prediction = toReuse ?? new HitPrediction();
            prediction.LKGX = movingObject.Left;
            prediction.LKGY = movingObject.Top;
            prediction.Visibility = visibility;

            if (visibility == 0)
            {
                prediction.Type = HitType.None;
                return prediction;
            }

            var mov = movingObject;

            var rayIndex = 0;
            castBuffer = castBuffer ??  new Edge[10000];
            if (mode == CastingMode.Precise)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;

                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Top, mov.Left + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Top, mov.Right + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Bottom, mov.Left + dx, mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Bottom, mov.Right + dx, mov.Bottom + dy);
                 

                var granularity = .5f;
                for (var x = mov.Left + granularity; x < mov.Left + mov.Width; x += granularity)
                {
                    castBuffer[rayIndex++] = new Edge( x,  mov.Top,  x+dx,  mov.Top+dy);
                    castBuffer[rayIndex++] = new Edge(x,  mov.Bottom, x + dx,  mov.Bottom + dy);
                }

                for (var y = mov.Top + granularity; y < mov.Top + mov.Height; y += granularity)
                {
                    castBuffer[rayIndex++] = new Edge(mov.Left, y, mov.Left+dx, y+dy);
                    castBuffer[rayIndex++] = new Edge(mov.Right,  y, mov.Right + dx, y + dy);
                }
            }
            else if (mode == CastingMode.Rough)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;

                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Top, mov.Left + dx,  mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right,  mov.Top, mov.Right + dx,  mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Left,  mov.Bottom, mov.Left + dx,  mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right,  mov.Bottom, mov.Right + dx,  mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.CenterX, mov.CenterY, mov.CenterX + dx,  mov.CenterY + dy);
            }
            else if(mode == CastingMode.SingleRay)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;
                castBuffer[rayIndex++] = new Edge(mov.CenterX,  mov.CenterY,  mov.CenterX + dx,  mov.CenterY + dy);
            }
            else
            {
                throw new NotSupportedException("Unknown mode: "+mode);
            }

            var closestIntersectionDistance = float.MaxValue;
            int closestIntersectingObstacleIndex = -1;
            Edge closestEdge = default;
            float closestIntersectionX = 0;
            float closestIntersectionY = 0;
            var len = bufferLen.HasValue ? bufferLen.Value : obstacles.Length;
            for (var i = 0; i < len; i++)
            {
                ref var obstacle = ref obstacles[i];
                if (visibility == float.MaxValue || movingObject.CalculateDistanceTo(obstacle) <= visibility)
                {
                    ProcessEdge(i, obstacle.TopEdge, rayIndex, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                    ProcessEdge(i, obstacle.BottomEdge, rayIndex, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                    ProcessEdge(i, obstacle.LeftEdge, rayIndex, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                    ProcessEdge(i, obstacle.RightEdge, rayIndex, edgesHitOutput, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                }
            }

            if(closestIntersectingObstacleIndex >= 0)
            {
                prediction.ObstacleHitBounds = obstacles[closestIntersectingObstacleIndex];
                prediction.ColliderHit = colliders == null ? null : colliders[closestIntersectingObstacleIndex];
                prediction.LKGD = closestIntersectionDistance - .1f;

                var lkg = movingObject.OffsetByAngleAndDistance(angle, prediction.LKGD, normalized: false);
                prediction.LKGX = lkg.Left;
                prediction.LKGY = lkg.Top;
                prediction.Type = HitType.Obstacle;
                prediction.Edge = closestEdge;
                prediction.IntersectionX = closestIntersectionX;
                prediction.IntersectionY = closestIntersectionY;
            }

            return prediction;
        }


        internal static HitPrediction PredictHitFast(ICollider c, RectF movingObject, RectF[] obstacles, Angle angle, ICollider[] colliders, float visibility, CastingMode mode, int bufferLen, HitPrediction toReuse)
        {
            var prediction = toReuse;
            prediction.LKGX = movingObject.Left;
            prediction.LKGY = movingObject.Top;
            prediction.Visibility = visibility;

            if (visibility == 0)
            {
                prediction.Type = HitType.None;
                return prediction;
            }

            var mov = movingObject;

            var rayIndex = 0;
            castBuffer = castBuffer ?? new Edge[10000];
            if (mode == CastingMode.Precise)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;

                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Top, mov.Left + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Top, mov.Right + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Bottom, mov.Left + dx, mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Bottom, mov.Right + dx, mov.Bottom + dy);


                var granularity = .5f;
                for (var x = mov.Left + granularity; x < mov.Left + mov.Width; x += granularity)
                {
                    castBuffer[rayIndex++] = new Edge(x, mov.Top, x + dx, mov.Top + dy);
                    castBuffer[rayIndex++] = new Edge(x, mov.Bottom, x + dx, mov.Bottom + dy);
                }

                for (var y = mov.Top + granularity; y < mov.Top + mov.Height; y += granularity)
                {
                    castBuffer[rayIndex++] = new Edge(mov.Left, y, mov.Left + dx, y + dy);
                    castBuffer[rayIndex++] = new Edge(mov.Right, y, mov.Right + dx, y + dy);
                }
            }
            else if (mode == CastingMode.Rough)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;

                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Top, mov.Left + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Top, mov.Right + dx, mov.Top + dy);
                castBuffer[rayIndex++] = new Edge(mov.Left, mov.Bottom, mov.Left + dx, mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.Right, mov.Bottom, mov.Right + dx, mov.Bottom + dy);
                castBuffer[rayIndex++] = new Edge(mov.CenterX, mov.CenterY, mov.CenterX + dx, mov.CenterY + dy);
            }
            else if (mode == CastingMode.SingleRay)
            {
                var delta = mov.OffsetByAngleAndDistance(angle, visibility, normalized: false);
                var dx = delta.Left - mov.Left;
                var dy = delta.Top - mov.Top;
                castBuffer[rayIndex++] = new Edge(mov.CenterX, mov.CenterY, mov.CenterX + dx, mov.CenterY + dy);
            }
            else
            {
                throw new NotSupportedException("Unknown mode: " + mode);
            }

            var closestIntersectionDistance = float.MaxValue;
            int closestIntersectingObstacleIndex = -1;
            Edge closestEdge = default;
            float closestIntersectionX = 0;
            float closestIntersectionY = 0;
            var len = bufferLen;

            for (var i = 0; i < len; i++)
            {
                ref var obstacle = ref obstacles[i];
                if (c == colliders[i] || c.CanCollideWith(colliders[i]) == false || colliders[i].CanCollideWith(c) == false) continue;
                if (visibility < float.MaxValue && RectF.CalculateDistanceTo(movingObject, obstacle) > visibility) continue;
               
                ProcessEdge(i, obstacle.TopEdge, rayIndex, null, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                ProcessEdge(i, obstacle.BottomEdge, rayIndex, null, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                ProcessEdge(i, obstacle.LeftEdge, rayIndex, null, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
                ProcessEdge(i, obstacle.RightEdge, rayIndex, null, visibility, ref closestIntersectionDistance, ref closestIntersectingObstacleIndex, ref closestEdge, ref closestIntersectionX, ref closestIntersectionY);
            }

            if (closestIntersectingObstacleIndex >= 0)
            {
                prediction.ObstacleHitBounds = obstacles[closestIntersectingObstacleIndex];
                prediction.ColliderHit = colliders == null ? null : colliders[closestIntersectingObstacleIndex];
                prediction.LKGD = closestIntersectionDistance - .1f;

                var lkg = movingObject.OffsetByAngleAndDistance(angle, prediction.LKGD, normalized: false);
                prediction.LKGX = lkg.Left;
                prediction.LKGY = lkg.Top;
                prediction.Type = HitType.Obstacle;
                prediction.Edge = closestEdge;
                prediction.IntersectionX = closestIntersectionX;
                prediction.IntersectionY = closestIntersectionY;
            }

            return prediction;
        }

        private static void ProcessEdge(int i, in Edge edge, int castLength, List<Edge> edgesHitOutput, float visibility, ref float closestIntersectionDistance, ref int closestIntersectingObstacleIndex, ref Edge closestEdge, ref float closestIntersectionX, ref float closestIntersectionY)
        {
            for (var k = 0; k < castLength; k++)
            {
                var ray = castBuffer[k];
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
    }
}