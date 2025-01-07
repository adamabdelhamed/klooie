using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.MemoryManagement)]
public class InnerLoopAPITests
{
    [TestMethod]
    public void InnerLoopAPIs_PauseResume()
    {
        Start = Stopwatch.GetTimestamp();
        var totalTestMilliseconds = 1000.0;
        var iterations = 10;
        var iterationStandardDelay = totalTestMilliseconds / iterations;

        // Constants for pause and resume timings
        var pauseDelay = ConsoleMath.Round(totalTestMilliseconds / 4);
        var resumeDelay = ConsoleMath.Round(totalTestMilliseconds / 2);

        ConsoleProvider.Current = new KlooieTestConsole();
        var loop = new Game();
        var times = new List<long>();

        loop.Invoke(() =>
        {
            loop.InnerLoopAPIs.For(iterations, iterationStandardDelay, (i) =>
            {
                Console.WriteLine($"InnerLoopAPIs.For iteration {i} at {NowString()}");
                times.Add(Stopwatch.GetTimestamp());
            }, loop.Stop);
        });

        loop.Invoke(async () =>
        {
            await Task.Delay(pauseDelay);
            Console.WriteLine($"Pausing event loop at {NowString()}");
            loop.Pause();
            await Task.Delay(resumeDelay);
            Console.WriteLine($"Resuming event loop at {NowString()}");
            loop.Resume();
            await Task.Delay(pauseDelay);
        });

        loop.Run();

        // Calculate delays between iterations
        var delays = new List<int>();
        for (int i = 1; i < times.Count; i++)
        {
            delays.Add(ConsoleMath.Round(TimeSpan.FromTicks(times[i] - times[i - 1]).TotalMilliseconds));
        }

        // Determine indices for assertions
        var pauseIndex = delays.Count / 4; // Approximate pause location based on timing
        var resumeIndex = pauseIndex + 1;

        // Check intervals before pause
        for (int i = 0; i < pauseIndex; i++)
        {
            Assert.IsTrue(Math.Abs(delays[i] - iterationStandardDelay) <= iterationStandardDelay * 0.1, $"Delay before pause for iteration {i} is out of expected range: {delays[i]} ms");
        }

        // Check pause gap
        var pauseGap = delays[pauseIndex];
        var expectedPauseGap = resumeDelay + iterationStandardDelay; // Includes resume delay
        Assert.IsTrue(pauseGap >= expectedPauseGap * 0.8, $"Pause gap is too short: {pauseGap} ms");

        // Check intervals after resume
        for (int i = resumeIndex; i < delays.Count; i++)
        {
            Assert.IsTrue(Math.Abs(delays[i] - iterationStandardDelay) <= iterationStandardDelay * 0.1, $"Delay after resume for iteration {i} is out of expected range: {delays[i]} ms");
        }
    }

    private static long Start;
    private static string NowString() => ConsoleMath.Round(TimeSpan.FromTicks(Stopwatch.GetTimestamp() - Start).TotalMilliseconds) + " ms";
}
