namespace klooie.Gaming;
/// <summary>
/// A movement that forcefully moves an element along a clear path
/// </summary>
public class Puppet : Movement
{
    private RectF destination;

    private Puppet(Velocity v, SpeedEval speed, RectF destination) : base(v, speed)
    {
        this.destination = destination;
    }

    /// <summary>
    /// Creates a new puppet movement
    /// </summary>
    /// <param name="v">the velocity to manipulate</param>
    /// <param name="speed">the movement speed</param>
    /// <param name="destination">the final location of the puppet</param>
    /// <returns>a task</returns>
    public static Movement Create(Velocity v, SpeedEval speed, RectF destination) => new Puppet(v, speed, destination);
    private float Now => ConsoleMath.Round(Game.Current.MainColliderGroup.Now.TotalSeconds, 1);

    protected override async Task Move()
    {
        var obstacles = Velocity.GetObstaclesSlow().Select(e => e.Bounds.Grow(.1f)).ToList();
        var from = Element.Bounds;
        var path = await Navigate.FindPathAdjusted(from, destination, obstacles);
        if (path == null) throw new Exception("No path");
        var speed = Speed();
        var start = Velocity.Group.Now;
        for(var i = 0; i < path.Count; i++)
        {
            var elapsedSeconds = Velocity.Group.Now - start;
            var stepsThatShouldHaveBeenTaken = Math.Ceiling(elapsedSeconds.TotalSeconds * speed);
            while(i >= stepsThatShouldHaveBeenTaken - 1)
            {
                Console.WriteLine($"{Now}: Waiting: i == {i}, expectedTaken == {stepsThatShouldHaveBeenTaken}");
                await Game.Current.DelayOrYield(100);
                elapsedSeconds = Velocity.Group.Now - start;
                stepsThatShouldHaveBeenTaken = Math.Ceiling(elapsedSeconds.TotalSeconds * speed);
            }
            Console.WriteLine($"{Now}: Moving: {i}");
            Element.MoveCenterTo(path[i].Left + .5f, path[i].Top + .5f);
        }
    }
}