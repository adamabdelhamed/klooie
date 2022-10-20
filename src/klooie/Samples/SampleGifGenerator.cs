using System.Reflection;

namespace klooie.Samples;

public interface IRecordableSample
{
    public int Width { get; }
    public int Height { get; }
    public string OutputPath { get; }
    public ConsoleApp Define();
}

public static class SampleGifGenerator
{
    public static void RunAll()
    {
        var samplesRoot = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(typeof(SampleGifGenerator).Assembly.Location))))),"Samples");
        var recordableSamples = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IRecordableSample)))
            .Select(t => Activator.CreateInstance(t) as IRecordableSample)
            .ToArray();
        foreach (var sample in recordableSamples)
        {
            var outputPath = Path.Combine(samplesRoot, sample.OutputPath);
            if (File.Exists(outputPath)) continue;

            Console.WindowWidth = sample.Width;
            Console.BufferWidth = sample.Width;
            Console.WindowHeight = sample.Height+1;
            var app = sample.Define();
            Record(app, outputPath);
            app.Run();
        }
    }

    private static void Record(ConsoleApp sampleApp, string outputPath)
    {
        sampleApp.LayoutRoot.EnableRecording(new ConsoleBitmapVideoWriter(videoText =>
        {
            var cvPath = outputPath.Replace(".gif", ".cv");
            File.WriteAllText(cvPath, videoText);
            using (var stream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(stream, leaveOpen:true))
                {
                    streamWriter.Write(videoText);
                }
                stream.Position = 0;

                var reader = new ConsoleBitmapStreamReader(stream);
                var video = reader.ReadToEnd();
                GifMaker.MakeGif(video, outputPath);
            }
        }));
    }
}
