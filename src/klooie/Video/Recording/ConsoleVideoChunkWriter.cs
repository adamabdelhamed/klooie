using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace klooie;

public enum ConsoleVideoFrameKind
{
    Raw,
    Diff,
}

public sealed class ConsoleVideoChunkWriter : IDisposable
{
    public const string Magic = "KLOOIE-CV2";
    public const string FooterPrefix = "KLOOIE-CV2-FOOTER";
    public const string FinalMarker = "KLOOIE-CV2-FINAL";

    private readonly FileInfo tempFile;
    private readonly FileInfo finalFile;
    private readonly int chunkIndex;
    private readonly TimeSpan chunkStart;
    private readonly RectF? window;
    private readonly FileStream stream;
    private readonly StreamWriter writer;
    private readonly ConsoleRecordingDiagnosticsBuilder diagnostics;
    private ConsoleCharacter[] previousPixels;
    private int previousWidth;
    private int previousHeight;
    private bool hasPrevious;
    private bool isFinished;
    private long frameCount;
    private TimeSpan firstFrameTime;
    private TimeSpan lastFrameTime;
    private int firstFrameWidth;
    private int firstFrameHeight;
    private int lastFrameWidth;
    private int lastFrameHeight;

    public FileInfo FinalFile => finalFile;
    public int ChunkIndex => chunkIndex;
    public TimeSpan ChunkStart => chunkStart;
    public long FrameCount => frameCount;

    internal ConsoleVideoChunkWriter(FileInfo tempFile, FileInfo finalFile, int chunkIndex, TimeSpan chunkStart, RectF? window, ConsoleRecordingDiagnosticsBuilder diagnostics)
    {
        this.tempFile = tempFile ?? throw new ArgumentNullException(nameof(tempFile));
        this.finalFile = finalFile ?? throw new ArgumentNullException(nameof(finalFile));
        this.chunkIndex = chunkIndex;
        this.chunkStart = chunkStart;
        this.window = window;
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        tempFile.Directory?.Create();
        stream = new FileStream(tempFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        writer = new StreamWriter(stream, Encoding.UTF8, 64 * 1024) { AutoFlush = false };
        WriteHeader();
    }

    public ConsoleVideoFrameKind WriteFrame(ConsoleBitmap bitmap, TimeSpan timestamp, bool forceRaw = false)
    {
        if (isFinished) throw new InvalidOperationException("Chunk writer has already been finished");
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

        var sw = Stopwatch.StartNew();
        var bytesBefore = stream.Position;
        var left = GetEffectiveLeft(bitmap);
        var top = GetEffectiveTop(bitmap);
        var width = GetEffectiveWidth(bitmap);
        var height = GetEffectiveHeight(bitmap);
        var pixelCount = checked(width * height);

        if (frameCount == 0)
        {
            firstFrameTime = timestamp;
            firstFrameWidth = width;
            firstFrameHeight = height;
        }

        var mustWriteRaw = forceRaw || hasPrevious == false || previousWidth != width || previousHeight != height;
        ConsoleVideoFrameKind kind;
        if (mustWriteRaw)
        {
            EnsurePreviousCapacity(pixelCount);
            WriteRawFrame(bitmap, timestamp, left, top, width, height);
            kind = ConsoleVideoFrameKind.Raw;
        }
        else
        {
            var diffCount = CountDiffs(bitmap, left, top, width, height);
            if (diffCount == 0)
            {
                diagnostics.RecordWriterLag(TimeSpan.Zero);
                return ConsoleVideoFrameKind.Diff;
            }

            if (diffCount > pixelCount / 2)
            {
                WriteRawFrame(bitmap, timestamp, left, top, width, height);
                kind = ConsoleVideoFrameKind.Raw;
            }
            else
            {
                WriteDiffFrame(bitmap, timestamp, left, top, width, height, diffCount);
                kind = ConsoleVideoFrameKind.Diff;
            }
        }

        frameCount++;
        lastFrameTime = timestamp;
        lastFrameWidth = width;
        lastFrameHeight = height;
        writer.Flush();
        sw.Stop();
        diagnostics.RecordFrame(kind, sw.Elapsed, stream.Position - bytesBefore);
        diagnostics.RecordWriterLag(sw.Elapsed);
        return kind;
    }

    public ConsoleRecordingChunkInfo Finish(TimeSpan duration)
    {
        if (isFinished) throw new InvalidOperationException("Chunk writer has already been finished");
        isFinished = true;

        var info = new ConsoleRecordingChunkInfo
        {
            ChunkIndex = chunkIndex,
            VideoPath = Path.Combine("chunks", finalFile.Name),
            ChunkStartTicks = chunkStart.Ticks,
            DurationTicks = duration.Ticks,
            FrameCount = frameCount,
            FirstFrameTicks = firstFrameTime.Ticks,
            LastFrameTicks = lastFrameTime.Ticks,
            FirstFrameWidth = firstFrameWidth,
            FirstFrameHeight = firstFrameHeight,
            LastFrameWidth = lastFrameWidth,
            LastFrameHeight = lastFrameHeight,
            FirstAudioSampleFrame = -1,
            Finalized = true,
        };

        writer.WriteLine(CreateFooterLine(info));
        writer.WriteLine(FinalMarker);
        writer.Flush();
        writer.Dispose();
        stream.Dispose();
        ReturnPreviousPixels();

        if (finalFile.Exists) finalFile.Delete();
        File.Move(tempFile.FullName, finalFile.FullName);
        diagnostics.RecordChunkFinalized();
        return info;
    }

    public void Dispose()
    {
        if (isFinished == false)
        {
            try { writer?.Dispose(); } catch { }
            try { stream?.Dispose(); } catch { }
            ReturnPreviousPixels();
        }
    }

    public static bool TryReadFinalizedChunkInfo(FileInfo file, out ConsoleRecordingChunkInfo info)
    {
        info = null;
        if (file == null || file.Exists == false) return false;

        string footer = null;
        string final = null;
        foreach (var line in File.ReadLines(file.FullName))
        {
            footer = final;
            final = line;
        }

        if (final != FinalMarker || footer == null || footer.StartsWith(FooterPrefix + "|", StringComparison.Ordinal) == false) return false;
        return TryParseFooterLine(footer, out info);
    }

    internal static string CreateFooterLine(ConsoleRecordingChunkInfo info) =>
        string.Join("|",
            FooterPrefix,
            info.ChunkIndex.ToString(CultureInfo.InvariantCulture),
            info.ChunkStartTicks.ToString(CultureInfo.InvariantCulture),
            info.DurationTicks.ToString(CultureInfo.InvariantCulture),
            info.FrameCount.ToString(CultureInfo.InvariantCulture),
            info.FirstFrameTicks.ToString(CultureInfo.InvariantCulture),
            info.LastFrameTicks.ToString(CultureInfo.InvariantCulture),
            info.FirstFrameWidth.ToString(CultureInfo.InvariantCulture),
            info.FirstFrameHeight.ToString(CultureInfo.InvariantCulture),
            info.LastFrameWidth.ToString(CultureInfo.InvariantCulture),
            info.LastFrameHeight.ToString(CultureInfo.InvariantCulture),
            info.FirstAudioSampleFrame.ToString(CultureInfo.InvariantCulture),
            info.AudioSampleCount.ToString(CultureInfo.InvariantCulture),
            info.AudioSampleRate.ToString(CultureInfo.InvariantCulture),
            info.AudioChannels.ToString(CultureInfo.InvariantCulture));

    internal static bool TryParseFooterLine(string line, out ConsoleRecordingChunkInfo info)
    {
        info = null;
        var parts = line.Split('|');
        if (parts.Length < 15 || parts[0] != FooterPrefix) return false;

        info = new ConsoleRecordingChunkInfo
        {
            ChunkIndex = int.Parse(parts[1], CultureInfo.InvariantCulture),
            ChunkStartTicks = long.Parse(parts[2], CultureInfo.InvariantCulture),
            DurationTicks = long.Parse(parts[3], CultureInfo.InvariantCulture),
            FrameCount = long.Parse(parts[4], CultureInfo.InvariantCulture),
            FirstFrameTicks = long.Parse(parts[5], CultureInfo.InvariantCulture),
            LastFrameTicks = long.Parse(parts[6], CultureInfo.InvariantCulture),
            FirstFrameWidth = int.Parse(parts[7], CultureInfo.InvariantCulture),
            FirstFrameHeight = int.Parse(parts[8], CultureInfo.InvariantCulture),
            LastFrameWidth = int.Parse(parts[9], CultureInfo.InvariantCulture),
            LastFrameHeight = int.Parse(parts[10], CultureInfo.InvariantCulture),
            FirstAudioSampleFrame = long.Parse(parts[11], CultureInfo.InvariantCulture),
            AudioSampleCount = long.Parse(parts[12], CultureInfo.InvariantCulture),
            AudioSampleRate = int.Parse(parts[13], CultureInfo.InvariantCulture),
            AudioChannels = int.Parse(parts[14], CultureInfo.InvariantCulture),
            Finalized = true,
        };
        return true;
    }

    private void WriteHeader()
    {
        writer.WriteLine(Magic);
        writer.WriteLine("ChunkIndex=" + chunkIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("ChunkStartTicks=" + chunkStart.Ticks.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("Encoding=TextV2");
        writer.WriteLine("KeyframePolicy=ChunkStartOnly");
        writer.WriteLine("ENDHEADER");
    }

    private void WriteRawFrame(ConsoleBitmap bitmap, TimeSpan timestamp, int left, int top, int width, int height)
    {
        writer.Write("R|");
        writer.Write(timestamp.Ticks.ToString(CultureInfo.InvariantCulture));
        writer.Write("|0|");
        writer.Write(width.ToString(CultureInfo.InvariantCulture));
        writer.Write("|");
        writer.Write(height.ToString(CultureInfo.InvariantCulture));
        writer.Write("|");

        var index = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (index > 0) writer.Write("~");
                var pixel = bitmap.GetPixel(left + x, top + y);
                previousPixels[index++] = pixel;
                WriteCell(pixel);
            }
        }
        writer.WriteLine();

        previousWidth = width;
        previousHeight = height;
        hasPrevious = true;
    }

    private void WriteDiffFrame(ConsoleBitmap bitmap, TimeSpan timestamp, int left, int top, int width, int height, int diffCount)
    {
        writer.Write("D|");
        writer.Write(timestamp.Ticks.ToString(CultureInfo.InvariantCulture));
        writer.Write("|0|");
        writer.Write(width.ToString(CultureInfo.InvariantCulture));
        writer.Write("|");
        writer.Write(height.ToString(CultureInfo.InvariantCulture));
        writer.Write("|");
        writer.Write(diffCount.ToString(CultureInfo.InvariantCulture));
        writer.Write("|");

        var emitted = 0;
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(left + x, top + y);
                if (pixel.EqualsIn(previousPixels[index]) == false)
                {
                    if (emitted++ > 0) writer.Write("~");
                    writer.Write(x.ToString(CultureInfo.InvariantCulture));
                    writer.Write(",");
                    writer.Write(y.ToString(CultureInfo.InvariantCulture));
                    writer.Write(",");
                    WriteCell(pixel);
                    previousPixels[index] = pixel;
                }
                index++;
            }
        }
        writer.WriteLine();
    }

    private int CountDiffs(ConsoleBitmap bitmap, int left, int top, int width, int height)
    {
        var diffs = 0;
        var index = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(left + x, top + y);
                if (pixel.EqualsIn(previousPixels[index]) == false) diffs++;
                index++;
            }
        }
        return diffs;
    }

    private void WriteCell(in ConsoleCharacter cell)
    {
        writer.Write(((int)cell.Value).ToString(CultureInfo.InvariantCulture));
        writer.Write(";");
        WriteRgb(cell.ForegroundColor);
        writer.Write(";");
        WriteRgb(cell.BackgroundColor);
        writer.Write(";");
        writer.Write(cell.IsUnderlined ? "1" : "0");
    }

    private void WriteRgb(in RGB rgb)
    {
        writer.Write(rgb.R.ToString(CultureInfo.InvariantCulture));
        writer.Write(",");
        writer.Write(rgb.G.ToString(CultureInfo.InvariantCulture));
        writer.Write(",");
        writer.Write(rgb.B.ToString(CultureInfo.InvariantCulture));
    }

    private int GetEffectiveLeft(ConsoleBitmap bitmap) => window.HasValue ? Math.Max(0, (int)window.Value.Left) : 0;
    private int GetEffectiveTop(ConsoleBitmap bitmap) => window.HasValue ? Math.Max(0, (int)window.Value.Top) : 0;
    private int GetEffectiveWidth(ConsoleBitmap bitmap) => window.HasValue ? Math.Min((int)window.Value.Width, bitmap.Width - GetEffectiveLeft(bitmap)) : bitmap.Width;
    private int GetEffectiveHeight(ConsoleBitmap bitmap) => window.HasValue ? Math.Min((int)window.Value.Height, bitmap.Height - GetEffectiveTop(bitmap)) : bitmap.Height;

    private void EnsurePreviousCapacity(int pixelCount)
    {
        if (previousPixels != null && previousPixels.Length >= pixelCount) return;
        ReturnPreviousPixels();
        previousPixels = ArrayPool<ConsoleCharacter>.Shared.Rent(pixelCount);
    }

    private void ReturnPreviousPixels()
    {
        if (previousPixels == null) return;
        ArrayPool<ConsoleCharacter>.Shared.Return(previousPixels);
        previousPixels = null;
    }
}
