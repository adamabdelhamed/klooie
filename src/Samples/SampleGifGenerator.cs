using ImageProcessor.Imaging.Quantizers;
using PowerArgs;
using System.Drawing;
using System.Drawing.Imaging;
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

                GifWriter.MakeGif(video, outputPath);
            }
        }));
    }
}

public class GifWriter : IDisposable
{
    #region Fields
    const long SourceGlobalColorInfoPosition = 10,
        SourceImageBlockPosition = 789;

    readonly BinaryWriter _writer;
    bool _firstFrame = true;
    readonly object _syncLock = new object();
    HashSet<RGB> colors;
    #endregion

    public static void MakeGif(InMemoryConsoleBitmapVideo video, string outputPath)
    {
        var colors = new HashSet<RGB>();
        foreach (var frame in video.Frames)
        {
            for(var x = 0; x < frame.Bitmap.Width; x++)
            {
                for (var y = 0; y < frame.Bitmap.Height; y++)
                {
                    var pixel = frame.Bitmap.GetPixel(x, y);
                    colors.Add(pixel.ForegroundColor);
                    colors.Add(pixel.BackgroundColor);
                }
            }
        }


        using (var outputStream = File.OpenWrite(outputPath))
        using (var gifWriter = new GifWriter(outputStream, colors))
        {
            for (int i = 0; i < video.Frames.Count; i++)
            {
                var frame = video.Frames[i];
                var nextFrame = i < video.Frames.Count - 1 ? video.Frames[i + 1] : null;
                var delay = nextFrame == null ? 0 : ConsoleMath.Round((nextFrame.FrameTime - frame.FrameTime).TotalMilliseconds);

                using (var bitmap = ToBitmap(frame.Bitmap))
                {
                    gifWriter.WriteFrame(bitmap, delay);
                }
            }
        }
    }

    public static Bitmap ToBitmap(ConsoleBitmap bitmap)
    {
        var b = new Bitmap(bitmap.Width * 10, bitmap.Height * 20);
        using (var g = Graphics.FromImage(b))
        {
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pix = bitmap.GetPixel(x, y);
                    var bgColor = Color.FromArgb(pix.BackgroundColor.R, pix.BackgroundColor.G, pix.BackgroundColor.B);
                    var fgColor = Color.FromArgb(pix.ForegroundColor.R, pix.ForegroundColor.G, pix.ForegroundColor.B);
                    var imgX = x * 10;
                    var imgY = y * 20;
                    g.FillRectangle(new SolidBrush(bgColor), imgX, imgY, 10, 20);
                    g.DrawString(pix.Value.ToString(), new Font("Consolas", 12), new SolidBrush(fgColor), imgX - 2, imgY);
                }
            }
        }
        return b;
    }


    public GifWriter(Stream OutStream, HashSet<RGB> colors, int DefaultFrameDelay = 500, int Repeat = 0)
    {
        if (OutStream == null)
            throw new ArgumentNullException(nameof(OutStream));

        if (DefaultFrameDelay <= 0)
            throw new ArgumentOutOfRangeException(nameof(DefaultFrameDelay));

        if (Repeat < -1)
            throw new ArgumentOutOfRangeException(nameof(Repeat));

        this.colors = colors;
        _writer = new BinaryWriter(OutStream);
        this.DefaultFrameDelay = DefaultFrameDelay;
        this.Repeat = Repeat;

    }

    #region Properties
    /// <summary>
    /// Gets or Sets the Default Width of a Frame. Used when unspecified.
    /// </summary>
    public int DefaultWidth { get; set; }

    /// <summary>
    /// Gets or Sets the Default Height of a Frame. Used when unspecified.
    /// </summary>
    public int DefaultHeight { get; set; }

    /// <summary>
    /// Gets or Sets the Default Delay in Milliseconds.
    /// </summary>
    public int DefaultFrameDelay { get; set; }

    /// <summary>
    /// The Number of Times the Animation must repeat.
    /// -1 indicates no repeat. 0 indicates repeat indefinitely
    /// </summary>
    public int Repeat { get; }
    #endregion

    /// <summary>
    /// Adds a frame to this animation.
    /// </summary>
    /// <param name="Image">The image to add</param>
    /// <param name="Delay">Delay in Milliseconds between this and last frame... 0 = <see cref="DefaultFrameDelay"/></param>
    public void WriteFrame(Image Image, int Delay = 0)
    {
        lock (_syncLock)
            using (var gifStream = new MemoryStream())
            {
                var quantizer = new OctreeQuantizer(255, 8);
                quantizer.Quantize(Image).Save(gifStream, ImageFormat.Gif);

                // Steal the global color table info
                if (_firstFrame)
                {
                    using (var tempStream = new MemoryStream())
                    {
                        CreateColorfulImage().Save(tempStream, ImageFormat.Gif);
                        InitHeader(tempStream, _writer, Image.Width, Image.Height);
                    }
                }

                WriteGraphicControlBlock(gifStream, _writer, Delay == 0 ? DefaultFrameDelay : Delay);
                WriteImageBlock(gifStream, _writer, !_firstFrame, 0, 0, Image.Width, Image.Height);
            }

        if (_firstFrame)
            _firstFrame = false;
    }

    Image CreateColorfulImage()
    {
        var w = colors.Count * 20;
        var h = 10;
        var quantizer = new OctreeQuantizer(255, 8);
        var bitmap = new Bitmap(w, h);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            int x = 0;
            int y = 0;
            foreach (var color in colors)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(color.R, color.G, color.B)), x, y, 20, 10);
                x += 20;
            }
        }
        return quantizer.Quantize(bitmap);
    }


    #region Write
    void InitHeader(Stream SourceGif, BinaryWriter Writer, int Width, int Height)
    {
        // File Header
        Writer.Write("GIF".ToCharArray()); // File type
        Writer.Write("89a".ToCharArray()); // File Version

        Writer.Write((short)(DefaultWidth == 0 ? Width : DefaultWidth)); // Initial Logical Width
        Writer.Write((short)(DefaultHeight == 0 ? Height : DefaultHeight)); // Initial Logical Height




        SourceGif.Position = SourceGlobalColorInfoPosition;
        var packedField = (byte)SourceGif.ReadByte();


        Writer.Write(packedField); // Global Color Table Info
        Writer.Write((byte)0); // Background Color Index
        Writer.Write((byte)0); // Pixel aspect ratio
        WriteColorTable(SourceGif, Writer);

        // App Extension Header for Repeating
        if (Repeat == -1)
            return;

        Writer.Write(unchecked((short)0xff21)); // Application Extension Block Identifier
        Writer.Write((byte)0x0b); // Application Block Size
        Writer.Write("NETSCAPE2.0".ToCharArray()); // Application Identifier
        Writer.Write((byte)3); // Application block length
        Writer.Write((byte)1);
        Writer.Write((short)Repeat); // Repeat count for images.
        Writer.Write((byte)0); // terminator
    }

    void WriteColorTable(Stream SourceGif, BinaryWriter Writer)
    {
        SourceGif.Position = 13; // Locating the image color table
        var colorTable = new byte[768];
        SourceGif.Read(colorTable, 0, colorTable.Length);
        Writer.Write(colorTable, 0, colorTable.Length);
    }

    static void WriteGraphicControlBlock(Stream SourceGif, BinaryWriter Writer, int FrameDelay)
    {
        SourceGif.Position = 781; // Locating the source GCE
        var blockhead = new byte[8];
        SourceGif.Read(blockhead, 0, blockhead.Length); // Reading source GCE

        Writer.Write(unchecked((short)0xf921)); // Identifier
        Writer.Write((byte)0x04); // Block Size
        Writer.Write((byte)(blockhead[3] & 0xf7 | 0x08)); // Setting disposal flag
        Writer.Write((short)(FrameDelay / 10)); // Setting frame delay
        Writer.Write(blockhead[6]); // Transparent color index
        Writer.Write((byte)0); // Terminator
    }

    void WriteImageBlock(Stream SourceGif, BinaryWriter Writer, bool IncludeColorTable, int X, int Y, int Width, int Height)
    {
        IncludeColorTable = true;
        SourceGif.Position = SourceImageBlockPosition; // Locating the image block
        var header = new byte[11];
        SourceGif.Read(header, 0, header.Length);
        Writer.Write(header[0]); // Separator
        Writer.Write((short)X); // Position X
        Writer.Write((short)Y); // Position Y
        Writer.Write((short)Width); // Width
        Writer.Write((short)Height); // Height

        if (IncludeColorTable) // If first frame, use global color table - else use local
        {
            SourceGif.Position = SourceGlobalColorInfoPosition;
            Writer.Write((byte)(SourceGif.ReadByte() & 0x3f | 0x80)); // Enabling local color table
            WriteColorTable(SourceGif, Writer);
        }
        else Writer.Write((byte)(header[9] & 0x07 | 0x07)); // Disabling local color table

        Writer.Write(header[10]); // LZW Min Code Size

        // Read/Write image data
        SourceGif.Position = SourceImageBlockPosition + header.Length;

        var dataLength = SourceGif.ReadByte();
        while (dataLength > 0)
        {
            var imgData = new byte[dataLength];
            SourceGif.Read(imgData, 0, dataLength);

            Writer.Write((byte)dataLength);
            Writer.Write(imgData, 0, dataLength);
            dataLength = SourceGif.ReadByte();
        }

        Writer.Write((byte)0); // Terminator
    }
    #endregion

    /// <summary>
    /// Frees all resources used by this object.
    /// </summary>
    public void Dispose()
    {
        // Complete File
        _writer.Write((byte)0x3b); // File Trailer

        _writer.BaseStream.Dispose();
        _writer.Dispose();
    }
}