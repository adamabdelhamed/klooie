using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
    private const int OutputWidth = 3840;
    private const int OutputHeight = 2160;
    private const double FinalFrameDurationSeconds = 1.0 / 30.0;
    private Font? cachedFont;
    private int cachedCellWidth;
    private int cachedCellHeight;

    public Task<FileInfo> ExportAsync(FileInfo manifestFile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Export(manifestFile, progress, cancellationToken), cancellationToken);

    public FileInfo Export(FileInfo manifestFile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (manifestFile == null) throw new ArgumentNullException(nameof(manifestFile));
        var session = new ConsoleRecordingSessionReader(manifestFile);
        var outputFile = new FileInfo(Path.ChangeExtension(manifestFile.FullName, ".mp4"));
        var workDirectory = new DirectoryInfo(Path.Combine(session.SessionDirectory.FullName, "export-frames"));
        if (workDirectory.Exists) workDirectory.Delete(recursive: true);
        workDirectory.Create();

        var framesConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "frames.ffconcat"));
        var audioConcatFile = new FileInfo(Path.Combine(workDirectory.FullName, "audio.ffconcat"));

        using var frameBuffer = new Bitmap(OutputWidth, OutputHeight);
        using var graphics = Graphics.FromImage(frameBuffer);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Rendering frames", OutputFile = outputFile });
        var renderedFrames = RenderFrames(session, workDirectory, framesConcatFile, frameBuffer, graphics, progress, outputFile, cancellationToken);
        if (renderedFrames == 0) throw new InvalidOperationException("Recording contains no frames to export");

        var hasAudio = WriteAudioConcat(session, audioConcatFile);
        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Running ffmpeg", FramesRendered = renderedFrames, OutputFile = outputFile });
        RunFfmpeg(framesConcatFile, audioConcatFile, outputFile, hasAudio, cancellationToken);
        progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Done", FramesRendered = renderedFrames, OutputFile = outputFile });
        return outputFile;
    }

    private int RenderFrames(ConsoleRecordingSessionReader session, DirectoryInfo workDirectory, FileInfo framesConcatFile, Bitmap frameBuffer, Graphics graphics, Action<ConsoleRecordingExportProgress>? progress, FileInfo outputFile, CancellationToken cancellationToken)
    {
        var frameIndex = 0;
        ConsoleBitmap? previousFrame = null;
        TimeSpan previousFrameTime = TimeSpan.Zero;

        using var concatWriter = new StreamWriter(framesConcatFile.FullName, append: false);
        concatWriter.WriteLine("ffconcat version 1.0");

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
                    WriteFrame(previousFrame, frameIndex, workDirectory, frameBuffer, graphics, concatWriter, duration.TotalSeconds);
                    frameIndex++;
                    previousFrame.Dispose(previousFrame.Lease, "Done exporting frame to MP4");
                    if (frameIndex % 25 == 0) progress?.Invoke(new ConsoleRecordingExportProgress { Stage = "Rendering frames", FramesRendered = frameIndex, OutputFile = outputFile });
                }

                previousFrame = reader.CurrentBitmap.Clone();
                previousFrameTime = reader.CurrentTimestamp;
            }
        }

        if (previousFrame != null)
        {
            WriteFrame(previousFrame, frameIndex, workDirectory, frameBuffer, graphics, concatWriter, FinalFrameDurationSeconds);
            AppendFinalConcatEntry(concatWriter, GetFrameFile(workDirectory, frameIndex));
            frameIndex++;
            previousFrame.Dispose(previousFrame.Lease, "Done exporting final frame to MP4");
        }

        return frameIndex;
    }

    private void WriteFrame(ConsoleBitmap bitmap, int frameIndex, DirectoryInfo workDirectory, Bitmap frameBuffer, Graphics graphics, StreamWriter concatWriter, double durationSeconds)
    {
        var frameFile = GetFrameFile(workDirectory, frameIndex);
        Rasterize(bitmap, frameBuffer, graphics);
        frameBuffer.Save(frameFile.FullName, ImageFormat.Png);
        AppendConcatEntry(concatWriter, frameFile, durationSeconds);
    }

    private void Rasterize(ConsoleBitmap bitmap, Bitmap frameBuffer, Graphics graphics)
    {
        graphics.Clear(Color.Black);
        EnsureFont(graphics, Math.Max(1, frameBuffer.Width / bitmap.Width), Math.Max(1, frameBuffer.Height / bitmap.Height));

        for (var x = 0; x < bitmap.Width; x++)
        {
            var left = x * frameBuffer.Width / bitmap.Width;
            var right = (x + 1) * frameBuffer.Width / bitmap.Width;
            var cellPixelWidth = right - left;

            for (var y = 0; y < bitmap.Height; y++)
            {
                var top = y * frameBuffer.Height / bitmap.Height;
                var bottom = (y + 1) * frameBuffer.Height / bitmap.Height;
                var cellPixelHeight = bottom - top;
                var cell = bitmap.GetPixel(x, y);
                var rect = new Rectangle(left, top, cellPixelWidth, cellPixelHeight);

                using var bg = new SolidBrush(Color.FromArgb(cell.BackgroundColor.R, cell.BackgroundColor.G, cell.BackgroundColor.B));
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.FillRectangle(bg, rect);

                if (cell.Value == ' ') continue;
                EnsureFont(graphics, cellPixelWidth, cellPixelHeight);

                using var fg = new SolidBrush(Color.FromArgb(cell.ForegroundColor.R, cell.ForegroundColor.G, cell.ForegroundColor.B));
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                var family = cachedFont!.FontFamily;
                var style = cachedFont.Style;
                var emHeight = family.GetEmHeight(style);
                var ascent = family.GetCellAscent(style);
                var ascentPixels = cachedFont.Size * ascent / emHeight;
                var drawX = rect.Left + rect.Width / 2f;
                var baselineY = rect.Top + rect.Height * 0.80f;
                var drawY = baselineY - ascentPixels;

                var glyphFormat = StringFormat.GenericTypographic;
                glyphFormat.Alignment = StringAlignment.Center;
                glyphFormat.LineAlignment = StringAlignment.Near;
                glyphFormat.FormatFlags |= StringFormatFlags.NoClip;

                graphics.DrawString(cell.Value.ToString(), cachedFont, fg, new PointF(drawX, drawY), glyphFormat);
            }
        }
    }

    private void EnsureFont(Graphics graphics, int cellWidth, int cellHeight)
    {
        if (cachedFont != null && cachedCellWidth == cellWidth && cachedCellHeight == cellHeight) return;
        cachedFont?.Dispose();

        var trialSize = (float)cellHeight;
        while (trialSize > 1)
        {
            var testFont = new Font("Consolas", trialSize, FontStyle.Regular, GraphicsUnit.Pixel);
            var size = graphics.MeasureString("W", testFont, PointF.Empty, StringFormat.GenericTypographic);
            if (size.Width <= cellWidth && size.Height <= cellHeight)
            {
                cachedFont = testFont;
                cachedCellWidth = cellWidth;
                cachedCellHeight = cellHeight;
                return;
            }

            testFont.Dispose();
            trialSize -= 0.5f;
        }

        cachedFont = new Font("Consolas", 1, FontStyle.Regular, GraphicsUnit.Pixel);
        cachedCellWidth = cellWidth;
        cachedCellHeight = cellHeight;
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

    private static FileInfo GetFrameFile(DirectoryInfo workDirectory, int frameIndex) => new FileInfo(Path.Combine(workDirectory.FullName, $"frame-{frameIndex:D06}.png"));

    private static void AppendConcatEntry(StreamWriter writer, FileInfo frameFile, double durationSeconds)
    {
        writer.WriteLine($"file '{EscapeFfconcatPath(frameFile)}'");
        writer.WriteLine($"duration {durationSeconds.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static void AppendFinalConcatEntry(StreamWriter writer, FileInfo frameFile) => writer.WriteLine($"file '{EscapeFfconcatPath(frameFile)}'");

    private static string EscapeFfconcatPath(FileInfo file) => file.FullName.Replace('\\', '/').Replace("'", "'\\''");
}
