using PowerArgs;

namespace klooie.VideoPlayer;
class Program
{
    [ArgRequired, ArgPosition(0)]
    public string InputPath { get; set; } = "";
    static void Main(string[] args) => Args.InvokeMain<Program>(args);
    public void Main() => new VideoPlayerApp().Run();
}

class VideoPlayerApp : ConsoleApp
{
    protected override Task Startup()
    {
        var input = Args.GetAmbientArgs<Program>().InputPath;
        var player = LayoutRoot
            .Add(new ConsoleBitmapPlayer())
            .Fill();
        player.AudioPlayback = new ConsoleRecordingAudioPlayer();

        if (File.Exists(input) && ConsoleRecordingManifestStore.IsManifestFile(new FileInfo(input)))
        {
            player.Load(new FileInfo(input));
        }
        else if (Directory.Exists(input))
        {
            player.Load(new DirectoryInfo(input));
        }
        else if (File.Exists(input))
        {
            player.Load(File.OpenRead(input));
        }
        else
        {
            throw new FileNotFoundException("Input path was not found", input);
        }

        return Task.CompletedTask;
    }
}

