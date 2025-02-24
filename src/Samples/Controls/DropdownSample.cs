//#Sample -Id DropdownSample
using PowerArgs;
namespace klooie.Samples;

public class DropdownSample : ConsoleApp
{
    private enum MyEnum
    {
        Value1,
        Value2,
        Value3
    }

    protected override async Task Startup()
    {
        var dropdown = LayoutRoot.Add(new EnumDropdown<MyEnum>()).DockToTop(padding:1).CenterHorizontally();

        await SendKey(ConsoleKey.Tab);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.Enter);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.DownArrow);
        await Task.Delay(1000);
        await SendKey(ConsoleKey.Enter);
        await Task.Delay(1000);
        await MessageDialog.Show(new ShowMessageOptions(ConsoleString.Parse($"Value: [B=Cyan][Black] {dropdown.Value} ")) { MaxLifetime = Task.Delay(3000).ToLifetime(), AllowEscapeToClose = false });
        Stop();
    }
}

// Entry point for your application
public static class DropdownSampleProgram
{
    public static void Main() => new DropdownSample().Run();
}
//#EndSample

public class DropdownSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\DropdownSample.gif";
    public int Width => 40;
    public int Height => 15;
    public ConsoleApp Define() => new DropdownSample();
 
}