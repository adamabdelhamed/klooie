using klooie;

var app = new ConsoleApp();
 
app.Invoke(async () =>
{
   while(true)
    {
        var control = ConsoleControlPool.Instance.Rent();
        app.LayoutRoot.Add(control);
        ConsoleControlPool.Instance.Return(control);
    }
});


app.Run();
 
 