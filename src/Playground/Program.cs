using klooie;
using klooie.Observability;
using PowerArgs;

var app = new ConsoleApp();
var pool = new ControlPool();
byte i = 0;
app.Invoke(async () =>
{
    for (i = 0; i < 255; i++)
    {
        var percent = i / 255.0f;
        var c = app.LayoutRoot.Add(pool.Rent()).Fill();
 
        await Task.Delay(100);
        c.Dispose();
        pool.Return(c);
    }
    app.Stop();
});


app.Run();

public class ControlPool : Pool<ConsoleControl>
{
    protected override ConsoleControl Factory() => new ConsoleControl();
}


/*
void AddTeamMember(Game game, bool left, int position)
{
    var x = left ? 2 : game.GameBounds.Right - 3;
    var y = position * 2;
    var angle = left ? Angle.Right : Angle.Left;
    var teamMember = CreateTeamMember(x, y, angle);
    teamMember.Background = left ? RGB.Orange : RGB.DarkGreen;
}

GameCollider CreateTeamMember(float x, float y, Angle angle)
{
    var teamMember = game.GamePanel.Add(GameColliderPool.Instance.Rent());
    teamMember.Velocity.OnCollision.SubscribeOnce(c => { teamMember.TryDispose();c.ColliderHit.TryDispose(); });
    teamMember.OnDisposed(() =>
    {
        GameColliderPool.Instance.Return(teamMember);
        CreateTeamMember(x, y, angle);
    });
    teamMember.Velocity.Speed = 40;
    teamMember.Velocity.Angle = angle;
    teamMember.MoveTo(x,y);
    return teamMember;
}
*/