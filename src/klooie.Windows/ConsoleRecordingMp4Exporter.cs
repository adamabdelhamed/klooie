using PowerArgs;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace klooie;

public sealed class ConsoleRecordingExportProgress
{
    public string Stage { get; set; } = "";
    public int FramesRendered { get; set; }
    public FileInfo? OutputFile { get; set; }
}



public sealed class ConsoleRecordingMp4Exporter
{
    private ConsoleRendererScaleProfile scaleProfile = null!;
    private ConsoleBitmapRasterizer rasterizer = null!;
    private const double FinalFrameDurationSeconds = 1.0 / 30.0;

    public FileInfo Export(FileInfo manifestFile, ConsoleRendererScaleProfile profile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (manifestFile == null) throw new ArgumentNullException(nameof(manifestFile));
        this.scaleProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.rasterizer?.Dispose();
        this.rasterizer = new ConsoleBitmapRasterizer(scaleProfile);
        var session = new ConsoleRecordingSessionReader(manifestFile);
        var outputFile = new FileInfo(Path.ChangeExtension(manifestFile.FullName, ".mp4"));
        var workDirectory = new DirectoryInfo(Path.Combine(session.SessionDirectory.FullName, "export-frames"));
        if (workDirectory.Exists) workDirectory.Delete(recursive: true);
        workDirectory.Create();

        var framesConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "frames.ffconcat"));
        var audioConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "audio.ffconcat"));

        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Rendering frames", OutputFile = outputFile });
        var renderedFrames = RenderFrames(session, workDirectory, framesConcatFile, progress, outputFile, cancellationToken);
        if (renderedFrames == 0) throw new InvalidOperationException("Recording contains no frames to export");

        var hasAudio = WriteAudioConcat(session, audioConcatFile);
        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Running ffmpeg", FramesRendered = renderedFrames, OutputFile = outputFile });
        RunFfmpeg(framesConcatFile, audioConcatFile, outputFile, hasAudio, cancellationToken);
        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Done", FramesRendered = renderedFrames, OutputFile = outputFile });
        return outputFile;
    }

    private int RenderFrames(ConsoleRecordingSessionReader session, DirectoryInfo workDirectory, FileInfo framesConcatFile, Action<ConsoleRecordingExportProgress>? progress, FileInfo outputFile, CancellationToken cancellationToken)
    {
        var outputWidth = session.Manifest.Chunks.First().FirstFrameWidth * scaleProfile.CellPixelWidth;
        var outputHeight = session.Manifest.Chunks.First().FirstFrameHeight * scaleProfile.CellPixelHeight;

        var frameBitmaps = new List<ConsoleBitmap>();
        var frameDurations = new List<double>();

        ConsoleBitmap? previousFrame = null;
        TimeSpan previousFrameTime = TimeSpan.Zero;

        foreach (var chunk in session.Manifest.Chunks.OrderBy(c => c.ChunkIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var reader = new ConsoleVideoChunkReader(session.ResolveChunkFile(chunk));

            while (reader.ReadFrame())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (previousFrame != null)
                {
                    var duration = reader.CurrentTimestamp - previousFrameTime;
                    if (duration <= TimeSpan.Zero) duration = TimeSpan.FromSeconds(FinalFrameDurationSeconds);

                    frameBitmaps.Add(previousFrame);
                    frameDurations.Add(duration.TotalSeconds);
                }

                previousFrame = reader.CurrentBitmap.Clone();
                previousFrameTime = reader.CurrentTimestamp;
            }
        }

        if (previousFrame != null)
        {
            frameBitmaps.Add(previousFrame);
            frameDurations.Add(FinalFrameDurationSeconds);
        }


        var renderedFrames = 0;
        var app = ConsoleApp.Current;
      
        using var rasterContext = new RasterContext(outputWidth, outputHeight);

        for (var i = 0; i < frameBitmaps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = frameBitmaps[i];
            WriteFrame(bitmap, i, workDirectory, rasterContext.FrameBuffer, rasterContext.Graphics);

            app?.Invoke(bitmap,static bitmap =>  bitmap.Dispose(bitmap.Lease, "Done exporting frame to MP4"));

            var completed = Interlocked.Increment(ref renderedFrames);
                 
            progress?.Invoke(new ConsoleRecordingExportProgress
            {
                Stage = "Rendering frames",
                FramesRendered = completed,
                OutputFile = outputFile
            });   
        }
    
        using var concatWriter = new StreamWriter(framesConcatFile.FullName, append: false);
        concatWriter.WriteLine("ffconcat version 1.0");

        for (var i = 0; i < frameDurations.Count; i++)
        {
            AppendConcatEntry(concatWriter, GetFrameFile(workDirectory, i), frameDurations[i]);
        }

        if (frameDurations.Count > 0)
        {
            AppendFinalConcatEntry(concatWriter, GetFrameFile(workDirectory, frameDurations.Count - 1));
        }

        return frameBitmaps.Count;
    }

    private void WriteFrame(ConsoleBitmap bitmap, int frameIndex, DirectoryInfo workDirectory, Bitmap frameBuffer, Graphics graphics)
    {
        var frameFile = GetFrameFile(workDirectory, frameIndex);
        rasterizer.Rasterize(bitmap, frameBuffer, graphics);
        frameBuffer.Save(frameFile.FullName, ImageFormat.Bmp);
    }


    private bool WriteAudioConcat(ConsoleRecordingSessionReader session, FileInfo audioConcatFile)
    {
        var audioFiles = session.Manifest.Chunks
            .OrderBy(c => c.ChunkIndex)
            .Where(c => string.IsNullOrWhiteSpace(c.AudioPath) == false)
            .Select(session.ResolveAudioFile)
            .Where(f => f.Exists)
            .ToList();

        if (audioFiles.Count == 0) return false;

        using var writer = new StreamWriter(audioConcatFile.FullName, append: false);
        writer.WriteLine("ffconcat version 1.0");

        for (var i = 0; i < audioFiles.Count; i++)
        {
            writer.WriteLine($"file '{EscapeFfconcatPath(audioFiles[i])}'");
        }

        return true;
    }

    private void RunFfmpeg(FileInfo framesConcatFile, FileInfo audioConcatFile, FileInfo outputFile, bool hasAudio, CancellationToken cancellationToken)
    {
        if (outputFile.Exists) outputFile.Delete();

        var startInfo = new ProcessStartInfo("ffmpeg")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("concat");
        startInfo.ArgumentList.Add("-safe");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(framesConcatFile.FullName);

        if (hasAudio)
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("concat");
            startInfo.ArgumentList.Add("-safe");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(audioConcatFile.FullName);
        }

        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");

        if (hasAudio)
        {
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add("-shortest");
        }

        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("veryfast");

        startInfo.ArgumentList.Add("-tune");
        startInfo.ArgumentList.Add("zerolatency");

        startInfo.ArgumentList.Add("-threads");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("-x264-params");
        startInfo.ArgumentList.Add("rc-lookahead=10");
        startInfo.ArgumentList.Add(outputFile.FullName);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffmpeg");

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        process.WaitForExit();
        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var stderr = stderrTask.GetAwaiter().GetResult();
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {stderr}");
        }
    }

    private static FileInfo GetFrameFile(DirectoryInfo workDirectory, int frameIndex) =>
        new FileInfo(Path.Combine(workDirectory.FullName, $"frame-{frameIndex:D06}.jpg"));

    private static void AppendConcatEntry(StreamWriter writer, FileInfo frameFile, double durationSeconds)
    {
        writer.WriteLine($"file '{EscapeFfconcatPath(frameFile)}'");
        writer.WriteLine($"duration {durationSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static void AppendFinalConcatEntry(StreamWriter writer, FileInfo frameFile) =>
        writer.WriteLine($"file '{EscapeFfconcatPath(frameFile)}'");

    private static string EscapeFfconcatPath(FileInfo file) =>
        file.FullName.Replace('\\', '/').Replace("'", "'\\''");

    private sealed class RasterContext : IDisposable
    {
        public Bitmap FrameBuffer { get; }
        public Graphics Graphics { get; }

        public RasterContext(int width, int height)
        {
            FrameBuffer = new Bitmap(width, height);
            Graphics = Graphics.FromImage(FrameBuffer);
            Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
            Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        }

        public void Dispose()
        {
            Graphics.Dispose();
            FrameBuffer.Dispose();
        }
    }
}
