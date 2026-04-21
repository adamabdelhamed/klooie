using System.Globalization;

namespace klooie;

public sealed class ConsoleVideoChunkReader : IDisposable
{
    private readonly StreamReader reader;
    private ConsoleBitmap readBuffer;
    private bool reachedEnd;

    public ConsoleRecordingChunkInfo ChunkInfo { get; }
    public ConsoleBitmap CurrentBitmap => readBuffer;
    public TimeSpan CurrentTimestamp { get; private set; }
    public bool CurrentFrameIsRaw { get; private set; }

    public ConsoleVideoChunkReader(FileInfo chunkFile)
    {
        if (chunkFile == null) throw new ArgumentNullException(nameof(chunkFile));
        if (ConsoleVideoChunkWriter.TryReadFinalizedChunkInfo(chunkFile, out var info) == false) throw new FormatException("Chunk is not finalized or has an invalid footer");
        ChunkInfo = info;
        reader = new StreamReader(File.OpenRead(chunkFile.FullName));
        ReadHeader();
    }

    public bool ReadFrame()
    {
        if (reachedEnd) return false;

        while (true)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                reachedEnd = true;
                return false;
            }

            if (line.StartsWith(ConsoleVideoChunkWriter.FooterPrefix + "|", StringComparison.Ordinal) || line == ConsoleVideoChunkWriter.FinalMarker)
            {
                reachedEnd = true;
                return false;
            }

            if (line.Length == 0) continue;
            ReadFrameLine(line);
            return true;
        }
    }

    public void Dispose()
    {
        reader.Dispose();
        readBuffer?.Dispose("external/klooie/src/klooie/Video/Recording/ConsoleVideoChunkReader.cs:1");
        readBuffer = null;
    }

    private void ReadHeader()
    {
        var magic = reader.ReadLine();
        if (magic != ConsoleVideoChunkWriter.Magic) throw new FormatException("Not a klooie cv2 chunk");

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line == "ENDHEADER") return;
        }
        throw new FormatException("Chunk header is incomplete");
    }

    private void ReadFrameLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length < 6) throw new FormatException("Invalid frame line");

        var isRaw = parts[0] == "R";
        var isDiff = parts[0] == "D";
        if (isRaw == false && isDiff == false) throw new FormatException("Unknown frame kind");

        CurrentFrameIsRaw = isRaw;
        CurrentTimestamp = new TimeSpan(long.Parse(parts[1], CultureInfo.InvariantCulture));
        var width = int.Parse(parts[3], CultureInfo.InvariantCulture);
        var height = int.Parse(parts[4], CultureInfo.InvariantCulture);

        if (isRaw)
        {
            EnsureBitmap(width, height, replace: true);
            var cells = parts[5].Length == 0 ? Array.Empty<string>() : parts[5].Split('~');
            if (cells.Length != width * height) throw new FormatException("Raw frame cell count mismatch");
            var index = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    readBuffer.SetPixel(x, y, ParseCell(cells[index++]));
                }
            }
        }
        else
        {
            EnsureBitmap(width, height, replace: false);
            var diffCount = int.Parse(parts[5], CultureInfo.InvariantCulture);
            if (diffCount == 0) return;
            var diffs = parts.Length > 6 && parts[6].Length > 0 ? parts[6].Split('~') : Array.Empty<string>();
            if (diffs.Length != diffCount) throw new FormatException("Diff frame cell count mismatch");
            foreach (var diff in diffs)
            {
                var comma1 = diff.IndexOf(',');
                var comma2 = diff.IndexOf(',', comma1 + 1);
                var x = int.Parse(diff.Substring(0, comma1), CultureInfo.InvariantCulture);
                var y = int.Parse(diff.Substring(comma1 + 1, comma2 - comma1 - 1), CultureInfo.InvariantCulture);
                var cell = ParseCell(diff.Substring(comma2 + 1));
                readBuffer.SetPixel(x, y, cell);
            }
        }
    }

    private void EnsureBitmap(int width, int height, bool replace)
    {
        if (replace || readBuffer == null || readBuffer.Width != width || readBuffer.Height != height)
        {
            readBuffer?.Dispose("external/klooie/src/klooie/Video/Recording/ConsoleVideoChunkReader.cs:2");
            readBuffer = ConsoleBitmap.Create(width, height);
        }
    }

    private static ConsoleCharacter ParseCell(string value)
    {
        var parts = value.Split(';');
        if (parts.Length != 4) throw new FormatException("Invalid cell");
        var ch = (char)int.Parse(parts[0], CultureInfo.InvariantCulture);
        var fg = ParseRgb(parts[1]);
        var bg = ParseRgb(parts[2]);
        var underlined = parts[3] == "1";
        return new ConsoleCharacter(ch, fg, bg, underlined);
    }

    private static RGB ParseRgb(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 3) throw new FormatException("Invalid RGB");
        return new RGB(
            byte.Parse(parts[0], CultureInfo.InvariantCulture),
            byte.Parse(parts[1], CultureInfo.InvariantCulture),
            byte.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
