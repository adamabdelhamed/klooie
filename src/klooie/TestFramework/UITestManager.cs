using System.Reflection;

namespace klooie.tests;

public enum UITestMode
{
    KeyFramesVerified,
    KeyFramesFYI,
    RealTimeFYI,
    Headless,
    HeadOnly,
}

public sealed class UITestManager
{
    private ConsoleBitmapVideoWriter keyFrameRecorder;
    private ConsoleApp app;
    private int keyFrameCount = 0;
    private string testId;
    public double SecondsBetweenKeyframes { get; set; } = 1;

    public static string GitRootPath
    {
        get
        {
            var gitRoot = Assembly.GetExecutingAssembly().Location;
            while (Directory.Exists(Path.Combine(gitRoot, ".git")) == false)
            {
                gitRoot = Path.GetDirectoryName(gitRoot);
            }

            return gitRoot;
        }
    }

    private string CurrentTestFYIRootPath => Path.Combine(GitRootPath, "FYI", testId);
    private string CurrentTestLKGRootPath => Path.Combine(GitRootPath, "LKGCliResults", testId);
    private string CurrentTestRootPath => mode == UITestMode.KeyFramesFYI || mode == UITestMode.RealTimeFYI ? CurrentTestFYIRootPath : CurrentTestLKGRootPath;
    private string CurrentTestLKGPath => Path.Combine(CurrentTestRootPath, "LKG");
    private string CurrentTestTempPath => Path.Combine(CurrentTestRootPath, "TEMP");
    private string CurrentTestRecordingFilePath => Path.Combine(CurrentTestTempPath, "Recording.cv");
    private string CurrentTestRecordingLKGFilePath => Path.Combine(CurrentTestLKGPath, "Recording.cv");
  
    private UITestMode mode;
 

    public UITestManager(ConsoleApp app, string testId, UITestMode mode)
    {
        if(mode == UITestMode.HeadOnly)
        {
            ConsoleProvider.Current = new StdConsoleProvider();
            return;
        }

        this.testId = testId;
        this.app = app;
        this.mode = mode;

        if (mode != UITestMode.Headless)
        {
            if (!Directory.Exists(CurrentTestLKGPath)) Directory.CreateDirectory(CurrentTestLKGPath);
            if (!Directory.Exists(CurrentTestTempPath)) Directory.CreateDirectory(CurrentTestTempPath);
        }

        if (mode == UITestMode.KeyFramesFYI || mode == UITestMode.KeyFramesVerified)
        {
            this.keyFrameRecorder = new ConsoleBitmapVideoWriter(s => File.WriteAllText(CurrentTestRecordingFilePath, s));
        }
        else if(mode == UITestMode.RealTimeFYI)
        {
            app.Invoke(() =>
            {
                app.LayoutRoot.EnableRecording(new ConsoleBitmapVideoWriter(s => File.WriteAllText(CurrentTestRecordingFilePath, s)));
            });
            
        }
    }

    public async Task PaintAndRecordKeyFrameAsync()
    {
        await app.RequestPaintAsync();
        await app.RequestPaintAsync();

        if (ConsoleApp.Current == app)
        {
            RecordKeyFrame();
        }
        else
        {
            app.Invoke(RecordKeyFrame);
        }
    }

    private bool TryGetLKGRecording(out ConsoleBitmapStreamReader reader) => TryGetRecording(CurrentTestRecordingLKGFilePath, out reader);

    private bool TryGetCurrentRecording(out ConsoleBitmapStreamReader reader) => TryGetRecording(CurrentTestRecordingFilePath, out reader);

    private void RecordKeyFrame() => keyFrameRecorder.WriteFrame(app.Bitmap, true, TimeSpan.FromSeconds(SecondsBetweenKeyframes * keyFrameCount++));

    public void Finish()
    {
        if (mode == UITestMode.HeadOnly) return;

        this.keyFrameRecorder?.Finish();

        if (mode == UITestMode.Headless) return;

        if (mode == UITestMode.KeyFramesFYI || mode == UITestMode.RealTimeFYI)
        {
            PromoteToLKGInternal();
            return;
        }

        if (mode == UITestMode.KeyFramesVerified)
        {
            PromoteIfAllFramesMatch();
            return;
        }
    }

    private void PromoteIfAllFramesMatch()
    {
        if (TryGetLKGRecording(out ConsoleBitmapStreamReader reader))
        {
            reader.InnerStream.Dispose();
            AssertMatchAll();
            Console.WriteLine("LKG matches");
            PromoteToLKGInternal();
        }
        else
        {
            Console.WriteLine("Orignial LKG");
            PromoteToLKGInternal();
        }
    }

    private void AssertMatchAll()
    {
        if (TryGetCurrentRecording(out ConsoleBitmapStreamReader currentReader) &&
            TryGetLKGRecording(out ConsoleBitmapStreamReader lkgReader))
        {
            var currentVideo = currentReader.ReadToEnd();
            var lkgVideo = lkgReader.ReadToEnd();
            currentReader.InnerStream.Close();
            lkgReader.InnerStream.Close();
            Assert.AreEqual(lkgVideo.Frames.Count, currentVideo.Frames.Count, "Frame count does not match");

            for (var i = 0; i < lkgVideo.Frames.Count; i++)
            {
                var lkgFrame = lkgVideo.Frames[i];
                var currentFrame = currentVideo.Frames[i];

                if (lkgFrame.Bitmap.Equals(currentFrame.Bitmap) == false)
                {
                    Assert.Fail("Frames do not match at index " + i);
                }
            }
        }
    }

    private void AssertMatchFirstAndLast()
    {
        if (TryGetCurrentRecording(out ConsoleBitmapStreamReader currentReader) &&
            TryGetLKGRecording(out ConsoleBitmapStreamReader lkgReader))
        {
            var currentVideo = currentReader.ReadToEnd();
            var lkgVideo = lkgReader.ReadToEnd();
            currentReader.InnerStream.Close();
            lkgReader.InnerStream.Close();

            var lkgFirstFrame = lkgVideo.Frames[0];
            var currentFirstFrame = currentVideo.Frames[0];

            var lkgLastFrame = lkgVideo.Frames[lkgVideo.Frames.Count - 1];
            var currentLastFrame = currentVideo.Frames[currentVideo.Frames.Count - 1];

            Assert.AreEqual(lkgFirstFrame.Bitmap, currentFirstFrame.Bitmap);
            Assert.AreEqual(lkgLastFrame.Bitmap, currentLastFrame.Bitmap);
        }
    }

    private bool TryGetRecording(string path, out ConsoleBitmapStreamReader recordingReader)
    {
        if (File.Exists(path) == false)
        {
            recordingReader = null;
            return false;
        }

        recordingReader = new ConsoleBitmapStreamReader(File.OpenRead(path));
        return true;
    }

    private void PromoteToLKGInternal()
    {
        if (Directory.Exists(CurrentTestLKGPath))
        {
            Directory.Delete(CurrentTestLKGPath, true);
        }

        Directory.Move(CurrentTestTempPath, CurrentTestLKGPath);
    }

    public Loc? Find(ConsoleString text, StringComparison comparison = StringComparison.InvariantCulture) => Find(text, comparison, true);
    public Loc? Find(string text, StringComparison comparison = StringComparison.InvariantCulture) => Find(text.ToConsoleString(), comparison, false);

    private Loc? Find(ConsoleString text, StringComparison comparison, bool requireStylesToBeEqual)
    {
        if (text.Contains("\n") || text.Contains("\r"))
        {
            throw new ArgumentException("Text cannot contain newline characters. This function searches the target bitmap line by line.");
        }

        for (var y = 0; y < app.Bitmap.Height; y++)
        {
            var line = ConsoleString.Empty;
            for (var x = 0; x < app.Bitmap.Width; x++)
            {
                var pixel = app.Bitmap.GetPixel(x, y);
                line += pixel.ToConsoleString();
            }

            int index;

            if (requireStylesToBeEqual)
            {
                index = line.IndexOf(text, comparison);
            }
            else
            {
                index = line.ToString().IndexOf(text.ToString(), comparison);
            }

            if (index >= 0)
            {
                return new Loc(index, y);
            }
        }

        return null;
    }

    private static class Assert
    {
        public static void AreEqual(object a, object b, string msg = "")
        {
            if (a == null && b != null) throw new Exception($"{a} != {b}, {msg}");
        }

        public static void Fail(string msg) => throw new Exception(msg);
    }
}
