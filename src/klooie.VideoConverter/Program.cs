using klooie;
using PowerArgs;

public class Program
{
    [ArgCantBeCombinedWith(nameof(WatchDirectory))]
    public string? InputPath { get; set; }

    [ArgCantBeCombinedWith(nameof(InputPath))]
    public string? WatchDirectory { get; set; } 

    public static void Main(string[] args)
    {
        if(args.Length == 0)
        {
            Console.Write("Enter args: ");
            args = Args.Convert(Console.ReadLine() ?? "-WatchDirectory .");
        }
        Args.InvokeMain<Program>(args);
    }

    public async Task Main()
    {
        if(InputPath == null && WatchDirectory == null)
        {
            Console.WriteLine("Either an input path or a watch directory must be specified.");
            return;
        }

        if (File.Exists(InputPath))
        {
            var converter = new ConsoleRecordingMp4Exporter();
            var info = converter.Export(new FileInfo(InputPath), ConsoleRendererScaleProfile.High, (p)=> Console.WriteLine($"{p.Stage}: {p.FramesRendered} frames rendered"));
            Console.WriteLine($"Exported to {info.FullName}");
            return;
        }

        while (Directory.Exists(WatchDirectory))
        {
            var count = 0;
            foreach(var manifestFile in new DirectoryInfo(WatchDirectory).GetFiles("*.krec", SearchOption.AllDirectories))
            {
                var mp4File = manifestFile.FullName.Replace(".krec",".mp4", StringComparison.OrdinalIgnoreCase);
                if (File.Exists(mp4File)) continue;
                count++;
                Console.WriteLine($"Processing {manifestFile.Name}...");
                try
                {
                    var converter = new ConsoleRecordingMp4Exporter();
                    var info = converter.Export(manifestFile, ConsoleRendererScaleProfile.Medium, (p)=> Console.WriteLine($"{p.Stage}: {p.FramesRendered}  frames rendered"));
                    Console.WriteLine($"Exported {manifestFile.Name} to {info.FullName}");
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error processing {manifestFile.Name}: {ex.Message}");
                }
            }
            if (count == 0)
            {
                Console.WriteLine("No .krec files found. Watching...");
                await Task.Delay(5000);
            }
        }
    }
}