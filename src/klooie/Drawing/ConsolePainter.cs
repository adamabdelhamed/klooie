using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal static class ConsolePainter
{ 
    public static readonly FrameRateMeter SkipRateMeter = new FrameRateMeter();
    private static readonly FastConsoleWriter fastConsoleWriter = new FastConsoleWriter();
    private static readonly ChunkAwarePaintBuffer paintBuilder = new ChunkAwarePaintBuffer();
    private static readonly List<Run> runsOnLine = new List<Run>(128);

    private static int lastBufferWidth;

    public static void HideCursor() => fastConsoleWriter.Write("\x1b[?25l".ToCharArray(), "\x1b[?25l".Length);
    public static void ShowCursor() => fastConsoleWriter.Write("\x1b[?25h".ToCharArray(), "\x1b[?25h".Length);
    public static void EnterAltScreen() => fastConsoleWriter.Write("\x1b[?1049h".ToCharArray(), "\x1b[?1049h".Length);
    public static void ExitAltScreen() => fastConsoleWriter.Write("\x1b[?1049l".ToCharArray(), "\x1b[?1049l".Length);

    public static void Paint(ConsoleBitmap bitmap)
    {
        if (Console.WindowHeight == 0) return;

        if (PaintQoS.CanPaint()  == false)
        {
            SkipRateMeter.Increment();
            ConsoleApp.Current?.WriteLine(SkipRateMeter.CurrentFPS+" Frames Dropped/S");
            return;
        }

        if (lastBufferWidth != Console.BufferWidth)
        {
            lastBufferWidth = Console.BufferWidth;
            Console.Clear();
        }

        int attempts = 0;
        while (true)
        {
            try
            {
                if (bitmap.Width == 0 || bitmap.Height == 0)
                {
                    paintBuilder.Clear();
                    PaintQoS.StartRecord();
                    fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                    PaintQoS.FinishRecord();
                    return;
                }

                paintBuilder.Clear();
   
                for (int y = 0; y < bitmap.Height; y++)
                {
                    runsOnLine.Clear();
                    int x = 0;
                    while (x < bitmap.Width)
                    {
                        ref var p = ref bitmap.Pixels[bitmap.IndexOf(x, y)];
                        var fg = p.ForegroundColor;
                        var bg = p.BackgroundColor;
                        bool under = p.IsUnderlined;
                        int start = x;
                        x++;
                        while (x < bitmap.Width)
                        {
                            ref var q = ref bitmap.Pixels[bitmap.IndexOf(x, y)];
                            if (q.ForegroundColor != fg || q.BackgroundColor != bg || q.IsUnderlined != under) break;
                            x++;
                        }
                        runsOnLine.Add(new Run(start, x - start, fg, bg, under));
                    }

                    for (int i = 0; i < runsOnLine.Count; i++)
                    {
                        var run = runsOnLine[i];
                        if (run.Underlined) paintBuilder.Append(Ansi.Text.UnderlinedOn);
                        Ansi.Cursor.Move.ToLocation(run.Start + 1, y + 1, paintBuilder);
                        Ansi.Color.Foreground.Rgb(run.FG, paintBuilder);
                        Ansi.Color.Background.Rgb(run.BG, paintBuilder);
                        paintBuilder.AppendRunFromPixels(bitmap.Pixels, bitmap.Width, run.Start, y, run.Length);
                        if (run.Underlined) paintBuilder.Append(Ansi.Text.UnderlinedOff);
                    }
                }

                Ansi.Cursor.Move.ToLocation(bitmap.Width - 1, bitmap.Height - 1, paintBuilder);
                PaintQoS.StartRecord();
                fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                PaintQoS.FinishRecord();
                break;
            }
            catch (IOException) when (attempts++ < 2) { continue; }
            catch (ArgumentOutOfRangeException) when (attempts++ < 2) { continue; }
        }
    }

    private static class PaintQoS
    {
        // Config
        private static double FrameBudgetMs = 1000.0 / LayoutRootPanel.MaxPaintRate;
        private static double EmaAlpha = 0.12;         // damping for EMA
        private static double BackpressureMs = 9.0;    // if EMA(write) exceeds this, consider host saturated
        private static double MaxSkipMs = 200.0;       // don't skip more than this at once

        // Live values
        private static double EmaWriteMs;              // smoothed write() time
        private static double LastWriteMs;             // last frame's write() time
        private static long Frames;                    // frames painted
        private static long lastTimestamp;

        private static long skipUntilTimestamp;        // stopwatch ticks when painting can resume

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void StartRecord() => lastTimestamp = Stopwatch.GetTimestamp();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void FinishRecord()
        {
            LastWriteMs = Stopwatch.GetElapsedTime(lastTimestamp).TotalMilliseconds;
            Frames++;

            // EMA without allocs
            if (EmaWriteMs <= 0) EmaWriteMs = LastWriteMs;
            else EmaWriteMs = (EmaAlpha * LastWriteMs) + ((1.0 - EmaAlpha) * EmaWriteMs);

            // backpressure detection
            var isBackPressured = EmaWriteMs > BackpressureMs;
            if (isBackPressured)
            {
                // Scale skip duration relative to how far we exceed budget
                double over = EmaWriteMs - BackpressureMs;
                // heuristic: skip at least one frame budget, then scale up
                double skipMs = FrameBudgetMs + over * 1.5;
                if (skipMs > MaxSkipMs) skipMs = MaxSkipMs;
                skipUntilTimestamp = Stopwatch.GetTimestamp() + (long)(skipMs * Stopwatch.Frequency / 1000.0);
            }
            else
            {
                // not backpressured, clear any skips
                skipUntilTimestamp = 0;
            }
        }

        public static bool CanPaint() => Stopwatch.GetTimestamp() >= skipUntilTimestamp;
    }

    private readonly struct Run
    {
        public readonly int Start;
        public readonly int Length;
        public readonly RGB FG;
        public readonly RGB BG;
        public readonly bool Underlined;

        public Run(int start, int length, RGB fg, RGB bg, bool underlined)
        {
            Start = start;
            Length = length;
            FG = fg;
            BG = bg;
            Underlined = underlined;
        }
    }

    private sealed class FastConsoleWriter
    {
        private readonly Stream _outputStream;
        private readonly byte[] _byteBuffer;
        private readonly Encoder _encoder;
        private readonly int _maxCharCount;
        private int _bufferPosition;

        public FastConsoleWriter(int bufferSize = 8192)
        {
            _outputStream = Console.OpenStandardOutput();
            _byteBuffer = new byte[bufferSize];
            _encoder = Encoding.UTF8.GetEncoder();
            _maxCharCount = Encoding.UTF8.GetMaxCharCount(bufferSize);
            _bufferPosition = 0;
        }

        public void Write(char[] buffer, int length)
        {
            int charsProcessed = 0;
            while (charsProcessed < length)
            {
                int charsToProcess = Math.Min(_maxCharCount, length - charsProcessed);

                bool completed;
                int bytesUsed;
                int charsUsed;

                _encoder.Convert(
                    buffer, charsProcessed, charsToProcess,
                    _byteBuffer, 0, _byteBuffer.Length,
                    false, out charsUsed, out bytesUsed, out completed);

                _outputStream.Write(_byteBuffer, 0, bytesUsed);
                charsProcessed += charsUsed;
            }
        }
    }

    private sealed class ChunkAwarePaintBuffer : PaintBuffer
    {
        public void AppendRunFromPixels(ConsoleCharacter[] pixels, int width, int startX, int y, int length)
        {
            EnsureBigEnough(Length + length);
            int idx = y * width + startX;
            for (int i = 0; i < length; i++)
            {
                Buffer[Length++] = pixels[idx + i].Value;
            }
        }
    }
}
