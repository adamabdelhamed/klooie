//#Sample -Id ScrollableSample
using PowerArgs;
namespace klooie.Samples;

public class ScrollableSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // this panel will scroll anything added to it's ScrollableContent property
        var scrollable = LayoutRoot.Add(new ScrollablePanel()).Fill(padding: new Thickness(2,2,1,1));
        var stack = scrollable.ScrollableContent.Add(new StackPanel() { Orientation = Orientation.Vertical, AutoSize = StackPanel.AutoSizeMode.Both });
        
        // We make sure that the scrollable content size is the same as the stack of controls. If we forget to do this then the
        // stack will outgrow the scrollable content and it won't scroll properly.
        stack.BoundsChanged.Sync(() => scrollable.ScrollableContent.ResizeTo(stack.Width, stack.Height), stack);

        for(var i = 0; i < 100; i++)
        {
            stack.Add(new Label($"Row {i}"));
        }

        // simulate the user moving focus to the scrollbar
        await SendKey(ConsoleKey.Tab);
        
        // simulate user manually scrolling down
        for (var i = 0; i < 100-scrollable.Height; i++)
        {
            await SendKey(ConsoleKey.DownArrow);
            await Task.Delay(50);
            var sb = scrollable.Descendents.WhereAs<Scrollbar>().ToArray();
        }

        await Task.Delay(1000);

        // simulate the user pressing Page Up to scroll up faster
        for (var i = 0; i < 10; i++)
        {
            await SendKey(ConsoleKey.PageUp);
            await Task.Delay(200);
            var sb = scrollable.Descendents.WhereAs<Scrollbar>().ToArray();
        }

        await Task.Delay(1000);

        // simulate the user pressing End to get back to the bottom
        await SendKey(ConsoleKey.End);
        await Task.Delay(1000);

        // simulate the user pressing Home to get back to the top
        await SendKey(ConsoleKey.Home);
        await Task.Delay(1000);

        Stop();
    }
}

// Entry point for your application
public static class ScrollableSampleProgram
{
    public static void Main() => new ScrollableSample().Run();
}
//#EndSample

public class ScrollableSampleRunner : IRecordableSample
{
    public string OutputPath => @"Containers\ScrollableSample.gif";
    public int Width => 60;
    public int Height => 20;
    public ConsoleApp Define() => new ScrollableSample();

}