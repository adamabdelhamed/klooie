using PowerArgs;

namespace klooie.VideoPlayer;
class Program
{
    [ArgRequired, ArgExistingFile, ArgPosition(0)]
    public string InputFile { get; set; }
    static void Main(string[] args) => Args.InvokeMain<Program>(args);
    public void Main() => new VideoPlayerApp().Run();
}

class VideoPlayerApp : ConsoleApp
{
    protected override Task Startup()
    {
        LayoutRoot
            .Add(new ConsoleBitmapPlayer())
            .Fill()
            .Load(File.OpenRead(Args.GetAmbientArgs<Program>().InputFile));
        return Task.CompletedTask;
    }
}

