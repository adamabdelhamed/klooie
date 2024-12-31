using klooie;
using PowerArgs;

var app = new ConsoleApp();
 
byte i = 0;
app.Invoke(async () =>
{
    var panel = new ConsolePanel() { Background = RGB.Green, Width = 10, Height = 5 };
    var c = panel.Add(new ConsoleControl() { Background = RGB.Red }).CenterBoth();
    app.LayoutRoot.Add(panel).CenterBoth();
});


app.Run();
 
 