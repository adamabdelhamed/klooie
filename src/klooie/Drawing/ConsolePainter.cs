using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal static class ConsolePainter
{ 
    public static readonly FrameRateMeter SkipRateMeter = new FrameRateMeter();
    private static readonly FastConsoleWriter fastConsoleWriter = new FastConsoleWriter();
    private static readonly ChunkAwarePaintBuffer paintBuilder = new ChunkAwarePaintBuffer();
    private static readonly List<Run> runsOnLine = new List<Run>(512);

    private static int lastBufferWidth;
    private static AnsiState _ansi;
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
        _ansi = default;
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
                    PaintQoS.BeginWrite();
                    fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                    PaintQoS.EndWrite(0);
                    return;
                }

                paintBuilder.Clear();
                int charBudget = PaintQoS.BudgetedCharsThisFrame();
                for (int y = 0; y < bitmap.Height; y++)
                {
                    if (paintBuilder.Length >= charBudget) break; // <-- hard cap per frame
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
                        if (paintBuilder.Length >= charBudget) break;
                        var run = runsOnLine[i];
                        EmitRunOptimized(bitmap, y, run, paintBuilder);
                    }
                }

                PaintQoS.BeginWrite();
                fastConsoleWriter.Write(paintBuilder.Buffer, paintBuilder.Length);
                PaintQoS.EndWrite(paintBuilder.Length);
                break;
            }
            catch (IOException) when (attempts++ < 2) { continue; }
            catch (ArgumentOutOfRangeException) when (attempts++ < 2) { continue; }
        }
    }

    private static class PaintQoS
    {
        // ---- Config (tune as needed) ----
        // Budget derived from your own MaxPaintRate
        private static double FrameBudgetMs = 1000.0 / LayoutRootPanel.MaxPaintRate;

        // EMA smoothing for write() cost and chars sent
        private static double EmaAlpha = 0.12;

        // Don’t skip so long that UX feels broken
        private static double MaxSkipMs = 180.0; // ~5.5 FPS floor

        // Optional “almost at budget” margin to start shedding work
        // e.g., start shedding if the smoothed write cost consistently exceeds 90% of budget
        private static double SheddingThresholdFactor = 0.90;

        // ---- Live state ----
        private static long _tsBegin;           // write start ticks
        private static double _emaWriteMs;        // smoothed write ms
        private static double _emaChars;          // smoothed chars/frame (proxy for bytes)
        private static double _debtMs;            // positive => we owe time to terminal
        private static long _skipUntilTicks;    // wall-clock gate to resume painting
        private static long _frames;            // painted frames
        private static long _lastCheckTicks;
        public static double EstimatedCharsPerMs => (_emaWriteMs > 0) ? (_emaChars / _emaWriteMs) : 0.0;

       // How many chars we can afford this frame (with a small headroom factor)
       public static int BudgetedCharsThisFrame(double headroom = 1.10)
       {
           var cpm = EstimatedCharsPerMs;
           if (cpm <= 0) return int.MaxValue; // no data yet, don’t cap early
           var maxChars = cpm * FrameBudgetMs * headroom;
           // never go to zero; allow a tiny minimum so the screen isn't frozen at start
           return (int) Math.Max(256, Math.Min(maxChars, int.MaxValue));
       }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void BeginWrite() => _tsBegin = Stopwatch.GetTimestamp();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void EndWrite(int charsWritten)
        {
            // Measure writer cost
            double writeMs = Stopwatch.GetElapsedTime(_tsBegin).TotalMilliseconds;
            _frames++;

            // Update EMAs
            if (_emaWriteMs <= 0) _emaWriteMs = writeMs;
            else _emaWriteMs = (EmaAlpha * writeMs) + ((1.0 - EmaAlpha) * _emaWriteMs);

            if (_emaChars <= 0) _emaChars = charsWritten;
            else _emaChars = (EmaAlpha * charsWritten) + ((1.0 - EmaAlpha) * _emaChars);

            // Compute budget overrun for this frame and accumulate "debt"
            // A negative delta reduces debt (we’re under budget), positive increases it.
            double deltaMs = writeMs - FrameBudgetMs;

            // Start shedding a bit earlier to avoid oscillation near the cliff
            double shedGuard = (SheddingThresholdFactor * FrameBudgetMs) - FrameBudgetMs; // negative number
            if (deltaMs > shedGuard) _debtMs += deltaMs;

            // Convert current debt to a skip window. We cap it to keep UX responsive.
            if (_debtMs > 0)
            {
                double skipMs = Math.Min(_debtMs, MaxSkipMs);

                // If writes are *way* over budget, scale the skip a bit more aggressively.
                // Example: if EMA is 2× budget, bump skip by extra 0.5× budget.
                double ratio = _emaWriteMs / FrameBudgetMs;
                if (ratio > 1.2) skipMs = Math.Min(skipMs + (0.5 * FrameBudgetMs), MaxSkipMs);

                _skipUntilTicks = Stopwatch.GetTimestamp() + (long)(skipMs * Stopwatch.Frequency / 1000.0);
            }
            else
            {
                // Clear gate if we’re not in debt
                _skipUntilTicks = 0;
            }
        }

        // Gate keeper. Lightweight and time-based.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool CanPaint()
        {
            long now = Stopwatch.GetTimestamp();
            if (_skipUntilTicks != 0 && now < _skipUntilTicks) return false;

            if (_lastCheckTicks != 0 && _debtMs > 0)
            {
                double elapsedMs = Stopwatch.GetElapsedTime(_lastCheckTicks, now).TotalMilliseconds;
                _debtMs -= elapsedMs;
                if (_debtMs < 0) _debtMs = 0;
            }
            _lastCheckTicks = now;

            return true;
        }
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

    private struct AnsiState
    {
        public RGB Fg, Bg;
        public bool Under;
        public int CursorX, CursorY;
        public bool HasColor; // first-run guard
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitRunOptimized(ConsoleBitmap bm, int y, Run run, ChunkAwarePaintBuffer pb)
    {
        ref var st = ref _ansi;

        // Cursor
        if (st.CursorX != run.Start + 1 || st.CursorY != y + 1)
        {
            Ansi.Cursor.Move.ToLocation(run.Start + 1, y + 1, pb);
            st.CursorX = run.Start + 1;
            st.CursorY = y + 1;
        }

        // Colors – force both on first use
        if (!st.HasColor)
        {
            Ansi.Color.Foreground.Rgb(run.FG, pb);
            Ansi.Color.Background.Rgb(run.BG, pb);
            st.Fg = run.FG;
            st.Bg = run.BG;
            st.HasColor = true;
        }
        else
        {
            if (run.FG != st.Fg)
            {
                Ansi.Color.Foreground.Rgb(run.FG, pb);
                st.Fg = run.FG;
            }
            if (run.BG != st.Bg)
            {
                Ansi.Color.Background.Rgb(run.BG, pb);
                st.Bg = run.BG;
            }
        }

        // Underline toggle
        if (run.Underlined != st.Under)
        {
            pb.Append(run.Underlined ? Ansi.Text.UnderlinedOn : Ansi.Text.UnderlinedOff);
            st.Under = run.Underlined;
        }

        pb.AppendRunFromPixels(bm.Pixels, bm.Width, run.Start, y, run.Length);
        st.CursorX += run.Length;
    }
}
