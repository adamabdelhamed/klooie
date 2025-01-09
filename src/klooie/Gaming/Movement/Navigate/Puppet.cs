namespace klooie.Gaming;
/// <summary>
/// A movement that forcefully moves an element along a clear path
/// </summary>
public class Puppet : Movement
{
    private RectF destination;
    private float closeEnough;

    private Puppet(Velocity v, SpeedEval speed, RectF destination, float closeEnough) : base(v, speed)
    {
        this.destination = destination;
        this.closeEnough = closeEnough;
    }

    /// <summary>
    /// Creates a new puppet movement
    /// </summary>
    /// <param name="v">the velocity to manipulate</param>
    /// <param name="speed">the movement speed</param>
    /// <param name="destination">the final location of the puppet</param>
    /// <returns>a task</returns>
    public static Movement Create(Velocity v, SpeedEval speed, RectF destination, float closeEnough = Mover.DefaultCloseEnough) => new Puppet(v, speed, destination, closeEnough);
 
    protected override async Task Move()
    {
        if (Element.CalculateNormalizedDistanceTo(destination) < closeEnough)
        {
            return;
        }

        var buffer = ObstacleBufferPool.Instance.Rent();
        Velocity.GetObstacles(buffer);
        try
        {
            var obstacles = buffer.ReadableBuffer.Where(o => o.Bounds != destination).Select(e => e.Bounds.Grow(.1f)).ToList();
            var from = Element.Bounds;
            var path = Navigate.FindPathAdjusted(from, destination, obstacles);
            if (path == null) throw new Exception("No path");
            var speed = Speed();
            var last = Velocity.Group.Now;
            var pathIndex = 0;

            if (Element.Bounds.Contains(path[0].ToRect(CollisionDetector.VerySmallNumber, CollisionDetector.VerySmallNumber)))
            {
                pathIndex++;
                if (pathIndex >= path.Count)
                {
                    return;
                }
            }

            while (true)
            {
                await Game.Current.DelayOrYield(10);
                var now = Velocity.Group.Now;
                var dt = (float)((now - last).TotalSeconds);
                last = now;

                var distanceToMoveBasedOnSpeed = dt * speed;
                var target = path[pathIndex];
                var angleToTarget = Element.Center().CalculateAngleTo(target);
                var distanceToTarget = Element.Center().CalculateNormalizedDistanceTo(target);

                //Console.WriteLine($"PathIndex: {pathIndex}, Angle: {angleToTarget}, Distance: {distanceToTarget}, DistanceToTravel: {distanceToMoveBasedOnSpeed}");

                if (distanceToMoveBasedOnSpeed > distanceToTarget - 1)
                {
                    //Console.WriteLine($"Path step reached");
                    Element.MoveTo(target.Left, target.Top);
                    pathIndex++;
                    if (pathIndex >= path.Count)
                    {
                        return;
                    }
                }
                else
                {
                    //Console.WriteLine($"Normal move");
                    Element.MoveByRadial(angleToTarget, distanceToMoveBasedOnSpeed);
                }

                if (Element.CalculateNormalizedDistanceTo(destination) < closeEnough)
                {
                    if (closeEnough == 0)
                    {
                        Element.MoveTo(destination.Left, destination.Top);
                    }
                    return;
                }
            }
        }finally
        {
            ObstacleBufferPool.Instance.Return(buffer);
        }
    }
}