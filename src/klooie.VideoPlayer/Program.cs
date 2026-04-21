using PowerArgs;
using System.Diagnostics;

namespace klooie.VideoPlayer;
class Program
{
    [ArgRequired, ArgPosition(0)]
    public string InputPath { get; set; } = "";
    static void Main(string[] args)
    {
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        Thread.CurrentThread.Name = "UI Thread";
        ConsoleProvider.Fancy = true;//ENABLE_VIRTUAL_TERMINAL_PROCESSING and such
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Args.InvokeMain<Program>(args);
    }
    public void Main() => new VideoPlayerApp().Run();
}

class VideoPlayerApp : ConsoleApp
{
    protected override Task Startup()
    {
        var input = Args.GetAmbientArgs<Program>().InputPath;
        FileInfo? recordingManifest = null;
        var exportRunning = false;
        var player = LayoutRoot
            .Add(new ConsoleBitmapPlayer())
            .Fill();
        player.AudioPlayback = new ConsoleRecordingAudioPlayer();

        if (File.Exists(input) && ConsoleRecordingManifestStore.IsManifestFile(new FileInfo(input)))
        {
            recordingManifest = new FileInfo(input);
            player.Load(recordingManifest);
        }
        else if (Directory.Exists(input))
        {
            var directory = new DirectoryInfo(input);
            recordingManifest = ConsoleRecordingManifestStore.GetManifestFile(directory);
            player.Load(directory);
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

