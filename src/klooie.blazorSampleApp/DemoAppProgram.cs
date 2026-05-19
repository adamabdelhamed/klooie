namespace klooie.blazorSampleApp;

public static class DemoAppProgram
{
    public static async Task MainAsync()
    {
        await new DemoApp().RunAsync();
    }
}
