using System.Globalization;

namespace klooie;

public sealed class ConsoleVideoChunkReader : IDisposable
{
    private readonly StreamReader reader;
    private readonly List<ConsoleCharacter> cellTable = new();
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
            if (line.StartsWith("T|", StringComparison.Ordinal))
            {
                ReadCellTableLine(line);
                continue;
            }

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

        var isRaw = parts[0] == "R" || parts[0] == "R3";
        var isDiff = parts[0] == "D" || parts[0] == "D3";
        var usesCellTable = parts[0] == "R3" || parts[0] == "D3";
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
                    readBuffer.SetPixel(x, y, usesCellTable ? ParseCellId(cells[index++]) : ParseCell(cells[index++]));
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
                var xText = diff.Substring(0, comma1);
                var yText = diff.Substring(comma1 + 1, comma2 - comma1 - 1);
                var x = usesCellTable ? FromBase36(xText) : int.Parse(xText, CultureInfo.InvariantCulture);
                var y = usesCellTable ? FromBase36(yText) : int.Parse(yText, CultureInfo.InvariantCulture);
                var cellText = diff.Substring(comma2 + 1);
                var cell = usesCellTable ? ParseCellId(cellText) : ParseCell(cellText);
                readBuffer.SetPixel(x, y, cell);
            }
        }
    }

    private void ReadCellTableLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 6) throw new FormatException("Invalid cell table line");

        var id = FromBase36(parts[1]);
        if (id != cellTable.Count) throw new FormatException("Cell table ids must be contiguous");

        cellTable.Add(new ConsoleCharacter(
            (char)FromBase36(parts[2]),
            ParseRgbHex(parts[3]),
            ParseRgbHex(parts[4]),
            parts[5] == "1"));
    }

    private ConsoleCharacter ParseCellId(string value)
    {
        var id = FromBase36(value);
        if ((uint)id >= (uint)cellTable.Count) throw new FormatException("Cell table id is out of range");
        return cellTable[id];
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

    private static RGB ParseRgbHex(string value)
    {
        if (value.Length != 6) throw new FormatException("Invalid RGB hex");
        return new RGB(
            byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static int FromBase36(string value)
    {
        var result = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var digit = c >= '0' && c <= '9'
                ? c - '0'
                : c >= 'A' && c <= 'Z'
                    ? c - 'A' + 10
                    : c >= 'a' && c <= 'z'
                        ? c - 'a' + 10
                        : -1;

            if (digit < 0 || digit >= 36) throw new FormatException("Invalid base36 value");
            checked { result = (result * 36) + digit; }
        }

        return result;
    }
}
