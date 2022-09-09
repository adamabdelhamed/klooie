using klooie;
using PowerArgs;

var app = new ConsoleApp();

app.Invoke(() =>
{
    var panel = app.LayoutRoot.Add(new BorderPanel() { Background = RGB.Red }).FillMax(maxWidth: 129, maxHeight: 40);
});

app.Run();
