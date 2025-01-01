using klooie.Gaming;
using PowerArgs;
var app = new Game();
 
app.Invoke(async () =>
{
 
    var control = GameColliderPool.Instance.Rent();
    control.Background = RGB.Red;
    app.LayoutRoot.Add(control);
    control.Velocity.Speed = 50;
    control.Velocity.Angle = 45;

});


app.Run();
 
 