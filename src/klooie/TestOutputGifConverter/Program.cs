// See https://aka.ms/new-console-template for more information
using klooie;
using PowerArgs;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Serialization;

var progressEvent = new Event<float>();
var root = @"C:\Users\adama\Source\Repos\klooie";
var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".cv", StringComparison.OrdinalIgnoreCase)).ToList();
//await GifMaker.ProcessRepo(root, files);

GifMaker.GenerateTestOutputPage(files, root);

public class GifMaker
{

    public static string GenerateTestOutputPage(List<string> files, string root)
    {
        var items = files.Select(f => GetOutputFileName(root, f))
            .Select(n => new Item()
            {
                DisplayName = Path.GetFileNameWithoutExtension(n).Replace("klooie.tests.","").Replace("ArgsTests.CLI.", ""),
                Id = Path.GetFileName(n),
                FileName = Path.GetFileName(n)
            }).ToList();

        var vm = new
        {
            Items = items,
        };

        var renderer = new DocumentRenderer();
        var ret = renderer.Render(Template, vm).StringValue;
        return ret;
    }

    public static Task ProcessRepo(string root, List<string> files)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var file in files)
                {
                    MakeGifFromRepoItem(root, file);
                    file.ToGreen().WriteLine();
                }
                tcs.SetResult();
            }
            catch(Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.IsBackground = false;
        thread.Start();
        return tcs.Task;
    }

    private static string GetOutputFileName(string repoRoot, string repoFile)
    {
        var testName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(repoFile)));
        var gifs = Path.Combine(repoRoot, "TestOutputGifs");
        if (Directory.Exists(gifs) == false) Directory.CreateDirectory(gifs);

        var outputPath = Path.Combine(gifs, testName + ".gif");
        return outputPath;
    }

    public static void MakeGifFromRepoItem(string repoRoot, string repoFile)
    {
        MakeGif(repoFile, GetOutputFileName(repoRoot, repoFile));
    }

    public static void MakeGif(string inputPath, string outputPath)
    {
        using (var stream = File.OpenRead(inputPath))
        {
            ConsoleBitmapStreamReader reader = new ConsoleBitmapStreamReader(stream);
            var video = reader.ReadToEnd();
            MakeGif(video, outputPath);
        }
    }

    public static void MakeGif(InMemoryConsoleBitmapVideo video, string outputPath) =>
        MakeGif(video.Frames, outputPath);

    public static void MakeGif(List<InMemoryConsoleBitmapFrame> frames, string outputPath)
    {
        const int PropertyTagLoopCount = 0x5101;
        const short PropertyTagTypeShort = 3;

        var gifEncoder = GetEncoder(ImageFormat.Gif);
        // Params of the first frame.
        var encoderParams1 = new EncoderParameters(1);
        encoderParams1.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        // Params of other frames.
        var encoderParamsN = new EncoderParameters(1);
        encoderParamsN.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
        // Params for the finalizing call.
        var encoderParamsFlush = new EncoderParameters(1);
        encoderParamsFlush.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);

        // PropertyItem for the number of animation loops.
        var loopPropertyItem = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
        loopPropertyItem.Id = PropertyTagLoopCount;
        loopPropertyItem.Type = PropertyTagTypeShort;
        loopPropertyItem.Len = 1; 
        // 0 means to animate forever.
        loopPropertyItem.Value = BitConverter.GetBytes((ushort)0);

        List<IDisposable> toDispose = new List<IDisposable>();
        using (var stream = new FileStream(outputPath, FileMode.Create))
        {
            bool first = true;
            Bitmap firstBitmap = null;
            // Bitmaps is a collection of Bitmap instances that'll become gif frames.
            foreach (var frame in frames)
            {
                var bitmap = ToBitmap(frame.Bitmap);
                toDispose.Add(bitmap);
                if (first)
                {
                    firstBitmap = bitmap;
                    firstBitmap.SetPropertyItem(CreateFrameDelay(frames));
                    firstBitmap.SetPropertyItem(loopPropertyItem);
                    firstBitmap.Save(stream, gifEncoder, encoderParams1);
                    first = false;
                }
                else
                {
                    firstBitmap.SaveAdd(bitmap, encoderParamsN);
                }
                
            }
            firstBitmap.SaveAdd(encoderParamsFlush);
        }
        toDispose.ForEach(d => d.Dispose());
    }

    private static PropertyItem CreateFrameDelay(List<InMemoryConsoleBitmapFrame> frames)
    {
        const int PropertyTagFrameDelay = 0x5100;
        const short PropertyTagTypeLong = 4;
        const int UintBytes = 4;

        // PropertyItem for the frame delay (apparently, no other way to create a fresh instance).
        var frameDelay = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
        frameDelay.Id = PropertyTagFrameDelay;
        frameDelay.Type = PropertyTagTypeLong;
        // Length of the value in bytes.
        frameDelay.Len = frames.Count * UintBytes;
        // The value is an array of 4-byte entries: one per frame.
        // Every entry is the frame delay in 1/100-s of a second, in little endian.
        frameDelay.Value = new byte[frames.Count * UintBytes];

        for (int j = 0; j < frames.Count; ++j)
        {
            uint frameTimeInHundredthsOfASecond = (uint)ConsoleMath.Round(frames[j].FrameTime.TotalSeconds * 100);
            uint previousFrameTimeInHundredthsOfASecond = j == 0 ? 0 : (uint)ConsoleMath.Round(frames[j-1].FrameTime.TotalSeconds * 100);
            var delay = frameTimeInHundredthsOfASecond - previousFrameTimeInHundredthsOfASecond;
            var frameDelayBytes = BitConverter.GetBytes(delay);
            Array.Copy(frameDelayBytes, 0, frameDelay.Value, j * UintBytes, UintBytes);
        }
        return frameDelay;
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return null;
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

    private class Item
    {
        public string FileName { get; set; }
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }

    private static string Template =
    @"
<!DOCTYPE html>
<html>
<head>
<title>Page Title</title>
</head>
<body>

<style>

.main 
{
  display: flex;
  height: 100%;
}

.col-1
{
  flex: 0 0 200px;
}

.col-2 {
  flex-grow: 1;
}

html,body
{
	width:100%;
	height: 100%;
	padding:0;
	margin:0;
    font-family:system-ui;
}

#theImage
{
margin-top: 50px;
}

ul
{
	list-style-type: none;
}

li
{
	color: white;
	cursor: pointer;
}

.testName:hover
{
	background-color: cyan;
	color:black;
}

</style>

<div class='main'>
	<div class='col-1' style='background: #333'>
		<div style='overflow-y:scroll;height:100%'>
		<ul>
            {{each item in Items}}
			<li><span data-url='https://adamabdelhamedpayg.blob.core.windows.net/powerargstestoutput/{{ item.FileName !}}' class='testName' id='{{ item.Id !}}' onclick='itemClicked(event,this)'>{{ item.DisplayName !}}</span></li>
            !{{each}}
            		</ul>
				</div>
	</div>
	<div class='col-2' style='background: #555;text-align:center'>
		<img style='vertical-align: middle;max-width: 600px;max-height: 500px;' id='theImage' src=''>
	</div>
  </div>

</div>

<script>

function itemClicked(ev,element)
{
	var url = element.getAttribute('data-url');
	document.getElementById('theImage').setAttribute('src',url);
}
</script>
</body>
</html>
    ";
}