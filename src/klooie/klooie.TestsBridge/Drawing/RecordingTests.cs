using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using PowerArgs;
using System.Threading;
using System.Diagnostics;
using System.Text;
using klooie.tests;

namespace ArgsTests.CLI.Recording
{
    [TestClass]
    [TestCategory(Categories.Drawing)]
    public class RecordingTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestRecordVideoBasic()
        {
            ConsoleBitmap bitmap = new ConsoleBitmap(4, 2), redBitmap = null, greenBitmap = null, magentaPixelBitmap = null;
            using (var sharedStream = new MemoryStream())
            {
                var bitmapVideoWriter = new ConsoleBitmapVideoWriter(s => sharedStream.Write(Encoding.Default.GetBytes(s)));
                
                bitmap = new ConsoleBitmap(4, 2);
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

        /// <summary>
        /// This test verifies that a large video can be read via the seek method quickly as long as the
        /// caller sends back the last frame index when they recall seek. Without this optimization this
        /// test should take a long time to run (almost a full second). With the optimization it should
        /// run in about a millisecond.
        /// </summary>
        [TestMethod, Timeout(1000)]
        public void TestRecordVideoLargeVideo()
        {
            ConsoleBitmap bitmap = new ConsoleBitmap(1, 1);
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
                while((lastFrameIndex =  video.Seek(destination, out frame, lastFrameIndex >= 0 ? lastFrameIndex : 0)) != numFrames - 1)
                {
                    destination = destination.Add(TimeSpan.FromMilliseconds(1));
                }
                sw.Stop();
                Assert.IsTrue(sw.ElapsedMilliseconds < 10);
                Console.WriteLine($"Playback took {sw.ElapsedMilliseconds} ms");
            }
        }

        [TestMethod]
        public void TestPlaybackEndToEndKeys() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, 80, 30, async (context) =>
        {
            int w = 10, h = 1;
            var temp = Path.GetTempFileName();
            using (var stream = File.OpenWrite(temp))
            {
                var writer = new ConsoleBitmapVideoWriter(s => stream.Write(Encoding.Default.GetBytes(s)));
                var bitmap = new ConsoleBitmap(w, h);

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
}
