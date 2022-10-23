//#Sample -Id MinimumSizeShieldSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

public class MinimumSizeShieldSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // create some contrast so we can see the effect
        LayoutRoot.Background = RGB.Orange.Darker;

        // in a real app the LayoutRoot itself is resizable by the user,
        // but for this sample we'll simulate the resize using a child panel
        var resizablePanel = LayoutRoot.Add(new ConsolePanel());
        resizablePanel.Width = 1;
        resizablePanel.Height = 1;

        // this app has a form that won't look good unless it has enough space
        resizablePanel.Add(new Form(FormGenerator.FromObject(new SampleFormModel())) { Width = 50, Height = 3 }).CenterBoth();

        // this shield ensures that we don't see the form unless it has enough room
        resizablePanel.Add(new MinimumSizeShield(new MinimumSizeShieldOptions()
        {
            MinHeight = 10,
            MinWidth = 50
        })).Fill();

        // now let's resize the panel and see how the behavior changes as it grows
        for(var w = 1; w < LayoutRoot.Width;w++)
        {
            resizablePanel.Width = w;
            await Task.Delay(30);
        }

        for (var h = 1; h < LayoutRoot.Height; h++)
        {
            resizablePanel.Height = h;
            await Task.Delay(200);
        }
        Stop();
    }
}

// Entry point for your application
public static class MinimumSizeShieldSampleProgram
{
    public static void Main() => new MinimumSizeShieldSample().Run();
}
//#EndSample

public class FMinimumSizeShieldSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\MinimumSizeShieldSample.gif";
    public int Width => 120;
    public int Height => 11;

    public ConsoleApp Define() => new MinimumSizeShieldSample();
}

// define a class where each property will map to a form input field
public class SampleFormModel : ObservableObject
{
    [FormWidth(25)] // this attribute controls the width of the input control
    [FormLabel("First Name")] // this attribute lets you customize the label
    public string FirstName { get => Get<string>(); set => Set(value); }

    [FormWidth(25)]
    [FormLabel("Last Name")]
    public string LastName { get => Get<string>(); set => Set(value); }
}