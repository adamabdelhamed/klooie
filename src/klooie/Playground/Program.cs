using klooie;
using PowerArgs;
using PowerArgs.Cli;

var app = new ConsoleApp();
app.Invoke(()=>
{ 
var camera = app.LayoutRoot.Add(new Camera()).Fill();
camera.EnableKeyboardPanning();
var small = camera.Add(new ConsoleControl() { Background = RGB.Red }).CenterBoth();
var slow = camera.Add(new SlowControl() { X = 2, Y = 2, Background = RGB.Green });
});
app.Run();

public class SlowControl : ConsoleControl
{
    protected override void OnPaint(ConsoleBitmap context)
    {
        Thread.Sleep(300);
    }
}