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
        var status = LayoutRoot.Add(new Label("Export MP4: E", autoSize: true) { Background = RGB.Black, Foreground = RGB.White }).DockToBottom();
        status.ZIndex = int.MaxValue;

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
            status.Text = "Legacy .cv playback; MP4 export requires .krec".ToConsoleString();
        }
        else
        {
            throw new FileNotFoundException("Input path was not found", input);
        }

        PushKeyForLifetime(ConsoleKey.E, () =>
        {
            if (recordingManifest == null)
            {
                status.Text = "MP4 export requires a .krec recording".ToConsoleString();
                return;
            }

            if (exportRunning) return;
            exportRunning = true;
            var manifestToExport = recordingManifest;
            status.Text = "Exporting MP4...".ToConsoleString(RGB.Yellow);
            var app = this;
            Task.Run(async () =>
            {
                try
                {
                    var exporter = new ConsoleRecordingMp4Exporter();
                    var output = await exporter.ExportAsync(manifestToExport, progress =>
                    {
                        app.Invoke(progress, progress =>
                        {
                            var frameText = progress.FramesRendered > 0 ? $" ({progress.FramesRendered} frames)" : "";
                            status.Text = $"{progress.Stage}{frameText}".ToConsoleString(RGB.Yellow);
                        });
                    });

                    app.Invoke(output, output =>
                    {
                        status.Text = $"Exported {output.FullName}".ToConsoleString(RGB.Green);
                    });
                }
                catch (Exception ex)
                {
                    app.Invoke(ex, ex =>
                    {
                        status.Text = "MP4 export failed".ToConsoleString(RGB.Red);
                        _ = MessageDialog.Show(ex.Message);
                    });
                }
                finally
                {
                    exportRunning = false;
                }
            });
        }, this);

        return Task.CompletedTask;
    }
}

