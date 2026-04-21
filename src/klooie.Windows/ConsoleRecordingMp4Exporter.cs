using PowerArgs;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
        TextScaleX = .95f,
        TextScaleY = 1f,
    };


    public static ConsoleRendererScaleProfile Low { get; } = new ConsoleRendererScaleProfile
    {
        Name = "Low",
        CellPixelWidth = 8,
        FontPixelSize = 14.5f,
        FontFamilyName = "Consolas",
        TextOffsetX = 0f,
        TextOffsetY = -3f,
        TextScaleX = 1.00f,
        TextScaleY = 0.947f,
    };
}

public sealed class ConsoleRecordingMp4Exporter
{
    private ConsoleRendererScaleProfile scaleProfile;
    private Font Font;
    private StringFormat glyphFormat;
    private readonly Dictionary<(char c, RGB color), Bitmap> tintedGlyphCache = new();

    private readonly Dictionary<RGB, SolidBrush> backgroundBrushCache = new();
    private readonly Dictionary<RGB, SolidBrush> foregroundBrushCache = new();
    private const double FinalFrameDurationSeconds = 1.0 / 30.0;
    private ConsoleGlyphAtlas glyphAtlas;
    private SolidBrush GetBackgroundBrush(RGB color)
    {
        if (backgroundBrushCache.TryGetValue(color, out var brush) == false)
        {
            brush = new SolidBrush(Color.FromArgb(color.R, color.G, color.B));
            backgroundBrushCache[color] = brush;
        }

        return brush;
    }

    public Task<FileInfo> ExportAsync(FileInfo manifestFile, ConsoleRendererScaleProfile profile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default) => Task.Run(() => Export(manifestFile, profile, progress, cancellationToken), cancellationToken);

    public FileInfo Export(FileInfo manifestFile, ConsoleRendererScaleProfile profile, Action<ConsoleRecordingExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (manifestFile == null) throw new ArgumentNullException(nameof(manifestFile));
        this.scaleProfile = profile ?? throw new ArgumentNullException(nameof(profile));
        this.Font = new Font(scaleProfile.FontFamilyName, scaleProfile.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
        this.glyphAtlas?.Dispose();
        this.glyphAtlas = new ConsoleGlyphAtlas(scaleProfile);
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
        frameBuffer.Save(frameFile.FullName, ImageFormat.Jpeg);
    }

    private void Rasterize(ConsoleBitmap bitmap, Bitmap frameBuffer, Graphics graphics)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        DrawBackgrounds(bitmap, graphics);
        DrawForegroundGlyphs(bitmap, graphics);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DrawBackgrounds(ConsoleBitmap bitmap, Graphics graphics)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var top = y * scaleProfile.CellPixelHeight;
            var runStart = 0;
            var runColor = bitmap.GetPixel(0, y).BackgroundColor;

            for (var x = 0; x < bitmap.Width; x++)
            {
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
    private void DrawForegroundGlyphs(ConsoleBitmap bitmap, Graphics graphics)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var top = y * scaleProfile.CellPixelHeight;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var cell = bitmap.GetPixel(x, y);
                if (cell.Value == ' ') continue;

                var tinted = GetTintedGlyph(cell.Value, cell.ForegroundColor);
                var left = x * scaleProfile.CellPixelWidth;
                graphics.DrawImageUnscaled(tinted, left - ConsoleGlyphAtlas.GlyphPaddingX, top);
            }
        }
    }

    private Bitmap GetTintedGlyph(char c, RGB color)
    {
        if (tintedGlyphCache.TryGetValue((c, color), out var tinted)) return tinted;

        var glyph = glyphAtlas.GetGlyph(c);
        tinted = TintGlyph(glyph, color);
        tintedGlyphCache[(c, color)] = tinted;
        return tinted;
    }

    private Bitmap TintGlyph(Bitmap source, RGB color)
    {
        var tinted = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(tinted);
        using var attributes = new ImageAttributes();

        var r = color.R / 255f;
        var gValue = color.G / 255f;
        var b = color.B / 255f;

        var matrix = new ColorMatrix(new[]
        {
        new[] { 0f, 0f, 0f, 0f, 0f },
        new[] { 0f, 0f, 0f, 0f, 0f },
        new[] { 0f, 0f, 0f, 0f, 0f },
        new[] { 0f, 0f, 0f, 1f, 0f },
        new[] { r,  gValue, b,  0f, 1f },
    });

        attributes.SetColorMatrix(matrix);

        g.DrawImage(
            source,
            new Rectangle(0, 0, source.Width, source.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);

        return tinted;
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

public sealed class ConsoleGlyphAtlas : IDisposable
{
    private readonly Dictionary<char, Bitmap> glyphs = new();
    private readonly Font font;
    private readonly StringFormat glyphFormat;
    private readonly ConsoleRendererScaleProfile scaleProfile;
    public int GlyphBitmapWidth => scaleProfile.CellPixelWidth + (GlyphPaddingX * 2);
    public int GlyphBitmapHeight => scaleProfile.CellPixelHeight + (GlyphPaddingY * 2);

    public const int GlyphPaddingX = 4;
    public const int GlyphPaddingY = 8;

    public ConsoleGlyphAtlas(ConsoleRendererScaleProfile scaleProfile)
    {
        this.scaleProfile = scaleProfile ?? throw new ArgumentNullException(nameof(scaleProfile));
        font = new Font(scaleProfile.FontFamilyName, scaleProfile.FontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel);

        glyphFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        glyphFormat.Alignment = StringAlignment.Near;
        glyphFormat.LineAlignment = StringAlignment.Near;
        glyphFormat.FormatFlags |= StringFormatFlags.NoClip | StringFormatFlags.MeasureTrailingSpaces;

        BuildBootstrapGlyphs();
    }

    public int CellPixelWidth => scaleProfile.CellPixelWidth;
    public int CellPixelHeight => scaleProfile.CellPixelHeight;

    public Bitmap GetGlyph(char c)
    {
        if (glyphs.TryGetValue(c, out var glyph)) return glyph;

        glyph = TryRenderGlyph(c);
        glyphs[c] = glyph;
        return glyph;
    }

    private Bitmap TryRenderGlyph(char c)
    {
        try
        {
            var glyph = RenderGlyph(c);
            if (GlyphHasVisiblePixels(glyph)) return glyph;

            glyph.Dispose();
            return glyphs['?'];
        }
        catch
        {
            return glyphs['?'];
        }
    }

    private static bool GlyphHasVisiblePixels(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A != 0) return true;
            }
        }

        return false;
    }

    private void BuildBootstrapGlyphs()
    {
        glyphs['?'] = RenderGlyph('?');
        glyphs[' '] = RenderGlyph(' ');
    }

    private Bitmap RenderGlyph(char c)
    {
        var bitmap = new Bitmap(GlyphBitmapWidth, GlyphBitmapHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.White);

        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        var oldTransform = graphics.Transform;
        try
        {
            graphics.TranslateTransform(GlyphPaddingX + scaleProfile.TextOffsetX, GlyphPaddingY + scaleProfile.TextOffsetY);
            graphics.ScaleTransform(scaleProfile.TextScaleX, scaleProfile.TextScaleY);
            graphics.DrawString(c.ToString(), font, brush, new PointF(0, 0), glyphFormat);
        }
        finally
        {
            graphics.Transform = oldTransform;
        }

        return bitmap;
    }

    public void Dispose()
    {
        foreach (var glyph in glyphs.Values)
        {
            glyph.Dispose();
        }

        glyphs.Clear();
        glyphFormat.Dispose();
        font.Dispose();
    }
}