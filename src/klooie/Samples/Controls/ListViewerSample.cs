//#Sample -Id ListViewerSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Description { get; set; }
}

public class ListViewerSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var data = Enumerable.Range(0, 100)
            .Select(i => new Person (){ FirstName = "First_" + i, LastName = "Last_" + i, Description = "Description_which_can_be_long" + i })
            .ToList();

        var listViewer = LayoutRoot.Add(new ListViewer<Person>(new ListViewerOptions<Person>()
        {
            DataSource = data,
            Columns = new List<HeaderDefinition<Person>>()
            {
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.FirstName.ToConsoleString().ToRed()),
                    Header = "First Name".ToYellow(),
                    Width = 1,
                    Type = GridValueType.RemainderValue,
                },
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.LastName.ToConsoleString().ToMagenta()),
                    Header = "Last Name".ToYellow(),
                    Width = 2,
                    Type = GridValueType.RemainderValue,
                }, 
                new HeaderDefinition<Person>()
                {
                    Formatter = (o) => new Label(o.Description.ToOrange()),
                    Header = "Description".ToYellow(),
                    Width = 5,
                    Type = GridValueType.RemainderValue,
                },
            }
        })).Fill();
        
        await Task.Delay(500);
        listViewer.Focus();

        for(var i = 0; i < 100; i++)
        {
            await SendKey(ConsoleKey.DownArrow);
            await Task.Delay(50);
        }



        Stop();
    }
}

// Entry point for your application
public static class ListViewerSampleProgram
{
    public static void Main() => new ListViewerSample().Run();
}
//#EndSample

public class ListViewerSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\ListViewerSample.gif";
    public int Width => 120;
    public int Height => 25;
    public ConsoleApp Define() => new ListViewerSample();
}