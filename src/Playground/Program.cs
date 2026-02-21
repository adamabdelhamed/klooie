using klooie;
using klooie.Gaming;
using PowerArgs;

// Test Case:
//
// Expected
// A ball flying towards a wall at a high rate of speed should stop right at the wall.
// 
// Actual
// The ball stops well short of the wall, indicating that its speed is not being properly accounted for in the collision detection logic. 
var game = new Game();
game.Invoke(async () =>
{
    var wall = game.GamePanel.Add(new GameCollider() { Background = RGB.Red, X = 150, Y = 0, Width = 10, Height = 200 });
    await Task.Delay(1000);
    var ball = game.GamePanel.Add(new GameCollider() { Background = RGB.Blue, X = 0, Y = 20, Width = 2, Height = 1 });
    await Task.Delay(1000);
    ball.Velocity.Angle = Angle.Right;
    ball.Velocity.Speed = 1000;
    ball.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Stop;
});
game.Run();