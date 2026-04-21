using PowerArgs;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace klooie;

public sealed class ConsoleRecordingExportProgress
{
    public string Stage { get; set; } = "";
    public int FramesRendered { get; set; }
    public FileInfo? OutputFile { get; set; }
}

public sealed class ConsoleRendererScaleProfile
{
    public required string Name { get; init; }
    public required int CellPixelWidth { get; init; }
    public required float FontPixelSize { get; init; }
    public required string FontFamilyName { get; init; }
    public required float TextOffsetX { get; init; }
    public required float TextOffsetY { get; init; }
    public required float TextScaleX { get; init; }
    public required float TextScaleY { get; init; }

    public int CellPixelHeight => CellPixelWidth * 2;

    public static ConsoleRendererScaleProfile High { get; } = new ConsoleRendererScaleProfile
    {
        Name = "High",
        CellPixelWidth = 32,
        FontPixelSize = 58f,
        FontFamilyName = "Consolas",
        TextOffsetX = 1f,
        TextOffsetY = -10f,
        TextScaleX = 1.003f,
        TextScaleY = 0.946f,
    };


    public static ConsoleRendererScaleProfile Low { get; } = new ConsoleRendererScaleProfile
    {
        Name = "Low",
        CellPixelWidth = 8,
        FontPixelSize = 14.5f,
        FontFamilyName = "Consolas",
        TextOffsetX = 0f,
        TextOffsetY = -3f,
        TextScaleX = 1.003f,
        TextScaleY = 0.947f,
    };
}

public sealed class ConsoleRecordingMp4Exporter
{
    private ConsoleRendererScaleProfile scaleProfile;
    private Font Font;
    private StringFormat glyphFormat;
    private Dictionary<RGB, char[]> rowBuffers = new Dictionary<RGB, char[]>();
    private Dictionary<RGB, StringBuilder> foregroundLayers = new Dictionary<RGB, StringBuilder>();
    private readonly Dictionary<RGB, StringBuilder> foregroundLayerCache = new();

    private readonly Dictionary<RGB, SolidBrush> backgroundBrushCache = new();
    private readonly Dictionary<RGB, SolidBrush> foregroundBrushCache = new();
    private const double FinalFrameDurationSeconds = 1.0 / 30.0;

    private SolidBrush GetBackgroundBrush(RGB color)
    {
        if (backgroundBrushCache.TryGetValue(color, out var brush) == false)
        {
            brush = new SolidBrush(Color.FromArgb(color.R, color.G, color.B));
            backgroundBrushCache[color] = brush;
        }

        return brush;
    }

    private SolidBrush GetForegroundBrush(RGB color)
    {
        if (foregroundBrushCache.TryGetValue(color, out var brush) == false)
        {
            brush = new SolidBrush(Color.FromArgb(color.R, color.G, color.B));
            foregroundBrushCache[color] = brush;
        }

        return brush;
    }

    public Task<FileInfo> ExportAsync(FileInfo manifestFile, ConsoleRendererScaleProfile profile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default) => Task.Run(() => Export(manifestFile, profile, progress, cancellationToken), cancellationToken);

    public FileInfo Export(FileInfo manifestFile, ConsoleRendererScaleProfile profile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (manifestFile == null) throw new ArgumentNullException(nameof(manifestFile));
        this.scaleProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.Font = new Font(scaleProfile.FontFamilyName, scaleProfile.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        var session = new ConsoleRecordingSessionReader(manifestFile);
        var outputFile = new FileInfo(Path.ChangeExtension(manifestFile.FullName, ".mp4"));
        var workDirectory = new DirectoryInfo(Path.Combine(session.SessionDirectory.FullName, "export-frames"));
        if (workDirectory.Exists) workDirectory.Delete(recursive: true);
        workDirectory.Create();

        var framesConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "frames.ffconcat"));
        var audioConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "audio.ffconcat"));

        glyphFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        glyphFormat.Alignment = StringAlignment.Near;
        glyphFormat.LineAlignment = StringAlignment.Near;
        glyphFormat.FormatFlags |= StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;

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
        Rasterize(bitmap, frameBuffer, graphics);
        frameBuffer.Save(frameFile.FullName, ImageFormat.Png);
    }

    private void Rasterize(ConsoleBitmap bitmap, Bitmap frameBuffer, Graphics graphics)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        foregroundLayers.Clear();
        FillBackgroundsAndDiscoverForegroundLayers(bitmap, graphics);

        if (foregroundLayers.Count == 0) return;

        rowBuffers.Clear();
        BuildForegroundLayerStrings(bitmap);
        DrawForegroundLayers(graphics);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FillBackgroundsAndDiscoverForegroundLayers(ConsoleBitmap bitmap, Graphics graphics)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var top = y * scaleProfile.CellPixelHeight;
            var runStart = 0;
            var runColor = bitmap.GetPixel(0, y).BackgroundColor;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var cell = bitmap.GetPixel(x, y);

                if (cell.Value != ' ' && foregroundLayers.ContainsKey(cell.ForegroundColor) == false)
                {
                    if (foregroundLayerCache.TryGetValue(cell.ForegroundColor, out var layer) == false)
                    {
                        layer = new StringBuilder((bitmap.Width + 1) * bitmap.Height);
                        foregroundLayerCache[cell.ForegroundColor] = layer;
                    }
                    else
                    {
                        layer.Clear();
                    }

                    foregroundLayers[cell.ForegroundColor] = layer;
                }

                var isLastCell = x == bitmap.Width - 1;
                var nextColorDifferent = isLastCell == false && bitmap.GetPixel(x + 1, y).BackgroundColor.Equals(runColor) == false;

                if (isLastCell || nextColorDifferent)
                {
                    var bg = GetBackgroundBrush(runColor);
                    graphics.FillRectangle(bg, runStart * scaleProfile.CellPixelWidth, top, (x - runStart + 1) * scaleProfile.CellPixelWidth, scaleProfile.CellPixelHeight);

                    if (isLastCell == false)
                    {
                        runStart = x + 1;
                        runColor = bitmap.GetPixel(runStart, y).BackgroundColor;
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildForegroundLayerStrings(ConsoleBitmap bitmap)
    {
        foreach (var color in foregroundLayers.Keys)
        {
            var row = new char[bitmap.Width];
            Array.Fill(row, ' ');
            rowBuffers[color] = row;
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            foreach (var row in rowBuffers.Values)
            {
                Array.Fill(row, ' ');
            }

            for (var x = 0; x < bitmap.Width; x++)
            {
                var cell = bitmap.GetPixel(x, y);
                if (cell.Value == ' ') continue;
                rowBuffers[cell.ForegroundColor][x] = cell.Value;
            }

            foreach (var pair in foregroundLayers)
            {
                pair.Value.Append(rowBuffers[pair.Key]);
                if (y < bitmap.Height - 1) pair.Value.Append('\n');
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrawForegroundLayers(Graphics graphics)
    {
        foreach (var pair in foregroundLayers)
        {
            var fg = GetForegroundBrush(pair.Key);
            var oldTransform = graphics.Transform;

            try
            {
                graphics.TranslateTransform(scaleProfile.TextOffsetX, scaleProfile.TextOffsetY);
                graphics.ScaleTransform(scaleProfile.TextScaleX, scaleProfile.TextScaleY);
                graphics.DrawString(pair.Value.ToString(), Font, fg, new PointF(0, 0), glyphFormat);
            }
            finally
            {
                graphics.Transform = oldTransform;
            }
        }
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
        new FileInfo(Path.Combine(workDirectory.FullName, $"frame-{frameIndex:D06}.png"));

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