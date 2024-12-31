using klooie;

var app = new ConsoleApp();
 
byte i = 0;
app.Invoke(async () =>
{
    for(var i = 0; i < 10000; i++)
    {
        var control = ConsoleControlPool.Instance.Rent();
        app.LayoutRoot.Add(control);
        ConsoleControlPool.Instance.Return(control);
    }
    app.Stop();
});


app.Run();
 
 