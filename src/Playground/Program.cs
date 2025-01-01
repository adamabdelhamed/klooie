using klooie.Gaming;
var app = new Game();
 
app.Invoke(async () =>
{
   while(true)
    {
        var control = GameColliderPool.Instance.Rent();
        app.LayoutRoot.Add(control);
        GameColliderPool.Instance.Return(control);
    }
});


app.Run();
 
 