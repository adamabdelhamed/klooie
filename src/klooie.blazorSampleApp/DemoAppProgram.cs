using klooie;

namespace klooie.blazorSampleApp;

public static class DemoAppProgram
{
    [KlooieWebTarget(DisplayName = "DemoApp", Description = "Animated labels rendered through the browser console host.")]
    public static async Task MainAsync()
    {
        await new DemoApp().RunAsync();
    }
}
