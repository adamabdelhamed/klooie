using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PowerArgs;
using System.Threading;
using System.Diagnostics;
using System.Text;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Drawing)]
public class RecordingTests
{
    public TestContext TestContext { get; set; }

    private static DirectoryInfo CreateRecordingDirectory()
    {
        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "klooie-recording-tests", Guid.NewGuid().ToString("N")));
        dir.Create();
        return dir;
    }

    private static ConsoleBitmap CreateFilledBitmap(int width, int height, ConsoleCharacter fill)
    {
        var bitmap = ConsoleBitmap.Create(width, height);
        bitmap.Fill(fill);
        return bitmap;
    }

    [TestMethod]
    public void ConsoleBitmapVideoWriter_Basic()
    {
        ConsoleBitmap bitmap = ConsoleBitmap.Create(4, 2), redBitmap = null, greenBitmap = null, magentaPixelBitmap = null;
        using (var sharedStream = new MemoryStream())
        {
            var bitmapVideoWriter = new ConsoleBitmapVideoWriter(s => sharedStream.Write(Encoding.Default.GetBytes(s)));

            bitmap = ConsoleBitmap.Create(4, 2);
            bitmap.Fill(ConsoleCharacter.RedBG());
            redBitmap = bitmapVideoWriter.WriteFrame(bitmap).Clone();
            bitmap.Fill(ConsoleCharacter.GreenBG());
            greenBitmap = bitmapVideoWriter.WriteFrame(bitmap).Clone();
            bitmap.DrawPoint(ConsoleCharacter.MagentaBG(), 0, 0);
            magentaPixelBitmap = bitmapVideoWriter.WriteFrame(bitmap).Clone();
            bitmapVideoWriter.Finish();

            sharedStream.Position = 0; // rewind the stream to the beginning to read it back

            // create a reader and make sure we can read each frame back exactly as they were written
            var bitmapVideoReader = new ConsoleBitmapStreamReader(sharedStream);
            Assert.AreEqual(redBitmap, bitmapVideoReader.ReadFrame().CurrentBitmap);
            Assert.AreEqual(greenBitmap, bitmapVideoReader.ReadFrame().CurrentBitmap);
            Assert.AreEqual(magentaPixelBitmap, bitmapVideoReader.ReadFrame().CurrentBitmap);
            Assert.IsNull(bitmapVideoReader.ReadFrame().CurrentFrame);
        }
    }

    [TestMethod]
    public void ConsoleRecordingSession_SingleChunkRoundTrips()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var timestamp = TimeSpan.Zero;
        var red = CreateFilledBitmap(4, 2, ConsoleCharacter.RedBG());
        var green = CreateFilledBitmap(4, 2, ConsoleCharacter.GreenBG());
        var magenta = green.Clone();
        magenta.DrawPoint(ConsoleCharacter.MagentaBG(), 0, 0);

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir, TimestampProvider = () => timestamp }))
        {
            session.WriteFrame(red);
            timestamp = TimeSpan.FromMilliseconds(100);
            session.WriteFrame(green);
            timestamp = TimeSpan.FromMilliseconds(200);
            session.WriteFrame(magenta);
            session.Stop();

            var diagnostics = session.Diagnostics;
            Assert.AreEqual(3, diagnostics.FrameCount);
            Assert.IsTrue(diagnostics.RawFrameCount >= 1);
            Assert.IsTrue(diagnostics.BytesWritten > 0);
            Assert.IsTrue(diagnostics.MaxWriteDuration >= TimeSpan.Zero);
        }

        var chunk = new FileInfo(Path.Combine(dir.FullName, "chunks", "chunk-000000.cv"));
        using var reader = new ConsoleVideoChunkReader(chunk);
        Assert.IsTrue(reader.ReadFrame());
        Assert.AreEqual(red, reader.CurrentBitmap);
        Assert.IsTrue(reader.CurrentFrameIsRaw);
        Assert.IsTrue(reader.ReadFrame());
        Assert.AreEqual(green, reader.CurrentBitmap);
        Assert.IsTrue(reader.ReadFrame());
        Assert.AreEqual(magenta, reader.CurrentBitmap);
        Assert.IsFalse(reader.ReadFrame());
    }

    [TestMethod]
    public void ConsoleRecordingSession_RollsChunksAndEachChunkStartsRaw()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var timestamp = TimeSpan.Zero;
        var bitmap = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir, ChunkDuration = TimeSpan.FromSeconds(1), TimestampProvider = () => timestamp }))
        {
            session.WriteFrame(bitmap);
            timestamp = TimeSpan.FromMilliseconds(500);
            bitmap.DrawPoint(ConsoleCharacter.GreenBG(), 0, 0);
            session.WriteFrame(bitmap);
            timestamp = TimeSpan.FromMilliseconds(1001);
            bitmap.DrawPoint(ConsoleCharacter.BlueBG(), 1, 0);
            session.WriteFrame(bitmap);
            session.Stop();
        }

        var chunk0 = new FileInfo(Path.Combine(dir.FullName, "chunks", "chunk-000000.cv"));
        var chunk1 = new FileInfo(Path.Combine(dir.FullName, "chunks", "chunk-000001.cv"));
        Assert.IsTrue(chunk0.Exists);
        Assert.IsTrue(chunk1.Exists);

        using var reader0 = new ConsoleVideoChunkReader(chunk0);
        Assert.IsTrue(reader0.ReadFrame());
        Assert.IsTrue(reader0.CurrentFrameIsRaw);

        using var reader1 = new ConsoleVideoChunkReader(chunk1);
        Assert.IsTrue(reader1.ReadFrame());
        Assert.IsTrue(reader1.CurrentFrameIsRaw);
    }

    [TestMethod]
    public void ConsoleRecordingSessionReader_ReadsChunkedSessionForPlayback()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var timestamp = TimeSpan.Zero;
        var bitmap = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir, ChunkDuration = TimeSpan.FromSeconds(1), TimestampProvider = () => timestamp }))
        {
            session.WriteFrame(bitmap);
            timestamp = TimeSpan.FromMilliseconds(500);
            bitmap.DrawPoint(ConsoleCharacter.GreenBG(), 0, 0);
            session.WriteFrame(bitmap);
            timestamp = TimeSpan.FromMilliseconds(1001);
            bitmap.DrawPoint(ConsoleCharacter.BlueBG(), 1, 0);
            session.WriteFrame(bitmap);
            session.Stop();
        }

        var manifest = ConsoleRecordingManifestStore.GetManifestFile(dir);
        Assert.IsTrue(manifest.Exists);
        Assert.AreEqual(ConsoleRecordingManifestStore.ManifestExtension, manifest.Extension);

        var reader = new ConsoleRecordingSessionReader(manifest);
        var video = reader.ReadToEnd();
        Assert.AreEqual(3, video.Frames.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1001), video.Frames[2].FrameTime);
        Assert.AreEqual(ConsoleCharacter.BlueBG(), video.Frames[2].Bitmap.GetPixel(1, 0));
        Assert.AreEqual(1, video.LoadProgress);
    }

    [TestMethod]
    public void ConsoleRecordingSession_FinalizesWhenTargetDisposes()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var bitmap = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());

        var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir });
        session.WriteFrame(bitmap);
        control.Dispose("test target disposal");

        var chunk = new FileInfo(Path.Combine(dir.FullName, "chunks", "chunk-000000.cv"));
        Assert.IsTrue(chunk.Exists);
        Assert.IsTrue(ConsoleVideoChunkWriter.TryReadFinalizedChunkInfo(chunk, out var info));
        Assert.AreEqual(1, info.FrameCount);
    }

    [TestMethod]
    public void ConsoleRecordingSession_ResizeEmitsRawFrame()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var timestamp = TimeSpan.Zero;
        var small = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());
        var large = CreateFilledBitmap(3, 2, ConsoleCharacter.GreenBG());

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir, TimestampProvider = () => timestamp }))
        {
            session.WriteFrame(small);
            timestamp = TimeSpan.FromMilliseconds(100);
            session.WriteFrame(large);
            session.Stop();
        }

        var chunk = new FileInfo(Path.Combine(dir.FullName, "chunks", "chunk-000000.cv"));
        using var reader = new ConsoleVideoChunkReader(chunk);
        Assert.IsTrue(reader.ReadFrame());
        Assert.AreEqual(2, reader.CurrentBitmap.Width);
        Assert.IsTrue(reader.CurrentFrameIsRaw);
        Assert.IsTrue(reader.ReadFrame());
        Assert.AreEqual(3, reader.CurrentBitmap.Width);
        Assert.AreEqual(large, reader.CurrentBitmap);
        Assert.IsTrue(reader.CurrentFrameIsRaw);
    }

    [TestMethod]
    public void ConsoleRecordingManifest_RebuildsFromFinalizedChunksWithoutManifest()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var bitmap = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir }))
        {
            session.WriteFrame(bitmap);
            session.Stop();
        }

        File.Delete(ConsoleRecordingManifestStore.GetManifestFile(dir).FullName);
        var rebuilt = ConsoleRecordingManifestStore.RebuildFromChunks(dir);
        Assert.AreEqual(1, rebuilt.Chunks.Count);
        Assert.AreEqual(0, rebuilt.Chunks[0].ChunkIndex);
        Assert.AreEqual(1, rebuilt.Chunks[0].FrameCount);
        Assert.IsTrue(rebuilt.Chunks[0].Finalized);
    }

    [TestMethod]
    public void ConsoleRecordingSession_WritesAlignedAudioChunks()
    {
        var dir = CreateRecordingDirectory();
        var control = new ConsoleControl();
        var timestamp = TimeSpan.Zero;
        var bitmap = CreateFilledBitmap(2, 2, ConsoleCharacter.RedBG());
        var samples = new float[SoundProvider.SampleRate * SoundProvider.ChannelCount / 10];
        for (var i = 0; i < samples.Length; i++) samples[i] = i % 2 == 0 ? .25f : -.25f;

        using (var session = ConsoleRecordingSession.Start(control, new ConsoleRecordingOptions { OutputDirectory = dir, ChunkDuration = TimeSpan.FromSeconds(1), TimestampProvider = () => timestamp }))
        {
            session.WriteFrame(bitmap);
            session.WriteAudioSamples(samples, SoundProvider.SampleRate, SoundProvider.ChannelCount, 0);
            session.Stop();
        }

        var audio = new FileInfo(Path.Combine(dir.FullName, "audio", "chunk-000000.wav"));
        Assert.IsTrue(audio.Exists);

        var manifest = ConsoleRecordingManifestStore.RebuildFromChunks(dir);
        Assert.AreEqual(1, manifest.Chunks.Count);
        Assert.AreEqual(Path.Combine("audio", "chunk-000000.wav"), manifest.Chunks[0].AudioPath);
        Assert.AreEqual(samples.Length, manifest.Chunks[0].AudioSampleCount);
        Assert.AreEqual(SoundProvider.SampleRate, manifest.Chunks[0].AudioSampleRate);
        Assert.AreEqual(SoundProvider.ChannelCount, manifest.Chunks[0].AudioChannels);
    }

    [TestMethod]
    public void ConsoleRecordingManifest_IgnoresIncompleteTempChunks()
    {
        var dir = CreateRecordingDirectory();
        var chunks = ConsoleRecordingSession.GetChunksDirectory(dir);
        chunks.Create();
        File.WriteAllText(Path.Combine(chunks.FullName, "chunk-000000.cv.tmp"), "not finalized");

        var rebuilt = ConsoleRecordingManifestStore.RebuildFromChunks(dir);
        Assert.AreEqual(0, rebuilt.Chunks.Count);
    }

    /// <summary>
    /// This test verifies that a large video can be read via the seek method quickly as long as the
    /// caller sends back the last frame index when they recall seek. Without this optimization this
    /// test should take a long time to run (almost a full second). With the optimization it should
    /// run in about a millisecond.
    /// </summary>
    [TestMethod, Timeout(1000)]
    public void ConsoleBitmapVideoWriter_Large()
    {
        ConsoleBitmap bitmap = ConsoleBitmap.Create(1, 1);
        var numFrames = 10000;
        using (var sharedStream = new MemoryStream())
        {
            var bitmapVideoWriter = new ConsoleBitmapVideoWriter(s => sharedStream.Write(Encoding.Default.GetBytes(s)));

            for (var i = 0; i < numFrames; i++)
            {
                bitmapVideoWriter.WriteFrame(bitmap, true, TimeSpan.FromMilliseconds(i));
            }
            bitmapVideoWriter.Finish();

            sharedStream.Position = 0; // rewind the stream to the beginning to read it back

            var destination = TimeSpan.Zero;

            var reader = new ConsoleBitmapStreamReader(sharedStream);
            var video = reader.ReadToEnd();
            var lastFrameIndex = 0;
            var sw = Stopwatch.StartNew();

            InMemoryConsoleBitmapFrame frame;
            while ((lastFrameIndex = video.Seek(destination, out frame, lastFrameIndex >= 0 ? lastFrameIndex : 0)) != numFrames - 1)
            {
                destination = destination.Add(TimeSpan.FromMilliseconds(1));
            }
            sw.Stop();
            Assert.IsTrue(sw.ElapsedMilliseconds < 10);
            Console.WriteLine($"Playback took {sw.ElapsedMilliseconds} ms");
        }
    }

    [TestMethod]
    public void ConsoleBitmapPlayer_TestPlaybackEndToEndKeys() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, 80, 30, async (context) =>
    {
        int w = 10, h = 1;
        var temp = Path.GetTempFileName();
        using (var stream = File.OpenWrite(temp))
        {
            var writer = new ConsoleBitmapVideoWriter(s => stream.Write(Encoding.Default.GetBytes(s)));
            var bitmap = ConsoleBitmap.Create(w, h);

            for (var i = 0; i < bitmap.Width; i++)
            {
                bitmap.Fill(new ConsoleCharacter(' '));
                bitmap.DrawPoint(new ConsoleCharacter(' ', backgroundColor: RGB.Red), i, 0);
                writer.WriteFrame(bitmap, true, TimeSpan.FromSeconds(.1 * i));
            }
            writer.Finish();
        }
        var app = ConsoleApp.Current;
        var player = app.LayoutRoot.Add(new ConsoleBitmapPlayer()).Fill();
        Assert.IsFalse(player.Width == 0);
        Assert.IsFalse(player.Height == 0);
        await context.PaintAndRecordKeyFrameAsync();
        await player.Load(File.OpenRead(temp));
        await context.PaintAndRecordKeyFrameAsync();
        await app.SendKey(ConsoleKey.P);
        await player.Stopped.CreateNextFireTask();
        await context.PaintAndRecordKeyFrameAsync();
        app.Stop();
    });
}
