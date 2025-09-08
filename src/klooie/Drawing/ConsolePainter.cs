using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class ConsolePainter
{ 
    public static int AvgWriteLength => FastConsoleWriter.AvgWriteLength;
    internal static readonly FrameRateMeter SkipRateMeter = new FrameRateMeter();
    private static readonly FastConsoleWriter fastConsoleWriter = new FastConsoleWriter();
    private static readonly ChunkAwarePaintBuffer paintBuilder = new ChunkAwarePaintBuffer();
    private static readonly List<Run> runsOnLine = new List<Run>(512);

    private static int lastBufferWidth;
    private static AnsiState _ansi;
    public static void HideCursor() => fastConsoleWriter.Write("\x1b[?25l".ToCharArray(), "\x1b[?25l".Length);
    public static void ShowCursor() => fastConsoleWriter.Write("\x1b[?25h".ToCharArray(), "\x1b[?25h".Length);
    public static void EnterAltScreen() => fastConsoleWriter.Write("\x1b[?1049h".ToCharArray(), "\x1b[?1049h".Length);
    public static void ExitAltScreen() => fastConsoleWriter.Write("\x1b[?1049l".ToCharArray(), "\x1b[?1049l".Length);

    public static bool Paint(ConsoleBitmap bitmap)
    {
        if (Console.WindowHeight == 0) return false;

        if (PaintQoS.CanPaint()  == false)
        {
            SkipRateMeter.Increment();
            ConsoleApp.Current?.WriteLine(SkipRateMeter.CurrentFPS+" Frames Dropped/S");
            return false;
        }
        _ansi = default;
        int colorThresholdSq = ComputeColorThresholdSq();
        BuildPerFrameThresholds(colorThresholdSq);
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
                    return false;
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

                            if (q.IsUnderlined != under) break;

                            bool fgOk, bgOk;

                            if (colorThresholdSq == 0)
                            {
                                fgOk = (q.ForegroundColor == fg);
                                bgOk = (q.BackgroundColor == bg);
                            }
                            else
                            {
                                int offset = x - start;
                                if (offset >= MaxSoftenedRun)
                                {
                                    // tolerance fully decayed -> strict equality
                                    fgOk = (q.ForegroundColor == fg);
                                    bgOk = (q.BackgroundColor == bg);
                                }
                                else
                                {
                                    int thrSq = s_ThrSqByOffset[offset];
                                    byte cap = s_ChannelCapByOffset[offset];

                                    // --- Space-aware: ignore FG when glyph is space (only BG is visible) ---
                                    if (q.Value == ' ')
                                    {
                                        // fast channel guard on BG before d² compare
                                        int dBr = q.BackgroundColor.R - bg.R; if ((dBr ^ (dBr >> 31)) > cap) break;
                                        int dBg = q.BackgroundColor.G - bg.G; if ((dBg ^ (dBg >> 31)) > cap) break;
                                        int dBb = q.BackgroundColor.B - bg.B; if ((dBb ^ (dBb >> 31)) > cap) break;

                                        bgOk = ColorsCloseEnough(q.BackgroundColor, bg, thrSq);
                                        fgOk = true; // ignored for spaces
                                    }
                                    else
                                    {
                                        // fast channel guards (FG + BG)
                                        int dFr = q.ForegroundColor.R - fg.R; if ((dFr ^ (dFr >> 31)) > cap) break;
                                        int dFg = q.ForegroundColor.G - fg.G; if ((dFg ^ (dFg >> 31)) > cap) break;
                                        int dFb = q.ForegroundColor.B - fg.B; if ((dFb ^ (dFb >> 31)) > cap) break;

                                        int dBr = q.BackgroundColor.R - bg.R; if ((dBr ^ (dBr >> 31)) > cap) break;
                                        int dBg = q.BackgroundColor.G - bg.G; if ((dBg ^ (dBg >> 31)) > cap) break;
                                        int dBb = q.BackgroundColor.B - bg.B; if ((dBb ^ (dBb >> 31)) > cap) break;

                                        fgOk = ColorsCloseEnough(q.ForegroundColor, fg, thrSq);
                                        bgOk = ColorsCloseEnough(q.BackgroundColor, bg, thrSq);
                                    }
                                }
                            }

                            if (!fgOk || !bgOk) break;
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
        return true;
    }

    // --- Tolerance decay ---
    // Q12 fixed-point multipliers (1.0 -> 4096). Shape ~exp decay to ~5% by step 24, ~2% by step 32.
    // You can tweak these 33 numbers; they’re intentionally conservative.
    private static readonly ushort[] s_ToleranceFalloffQ12 =
    {
    4096, 3600, 3162, 2779, 2440, 2144, 1887, 1659, 1460, 1286, 1134,
    1000,  882,  778,  686,  606,  535,  472,  416,  366,  322,
     284,  250,  221,  195,  172,  151,  133,  117,  102,   89,   77,   66, // ~1.6% at 32
};

    // Precomputed per-frame thresholds (squared) and per-channel caps (sqrt) for offsets 0..MaxSoftenedRun-1
    private static readonly int[] s_ThrSqByOffset = new int[MaxSoftenedRun];
    private static readonly byte[] s_ChannelCapByOffset = new byte[MaxSoftenedRun];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntSqrt(int x)
    {
        // Tiny, branchy integer sqrt for small ranges; good enough for caps (0..~112)
        if (x <= 0) return 0;
        int r = (int)Math.Sqrt(x); // if you want, replace with a branchless int-sqrt; this is fine once per frame*32
        return r;
    }

    private static void BuildPerFrameThresholds(int baseSq)
    {
        for (int i = 0; i < MaxSoftenedRun; i++)
        {
            int m = s_ToleranceFalloffQ12[i];                  // Q12
            int thrSq = baseSq <= 0 ? 0 : (int)(((long)baseSq * m) >> 12);
            s_ThrSqByOffset[i] = thrSq;

            // per-channel guard: if any |d| > cap then d² > thrSq → early fail
            int cap = IntSqrt(thrSq);
            if (cap > 255) cap = 255;
            s_ChannelCapByOffset[i] = (byte)cap;
        }
    }

    // After this many characters in the SAME run, treat tolerance as zero (exact equality only).
    private const int MaxSoftenedRun = 32;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecayedThresholdSq(int baseSq, int offsetInRun)
    {
        if (baseSq <= 0) return 0;
        if (offsetInRun >= MaxSoftenedRun) return 0;
        // Fixed-point multiply: (baseSq * falloffQ12) >> 12
        int m = s_ToleranceFalloffQ12[offsetInRun]; // 0..4096
        return (int)(((long)baseSq * m) >> 12);
    }

    private const int MaxRgbDistSq = 255 * 255 * 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ColorsCloseEnough(in RGB a, in RGB b, int maxDistSq)
    {
        // Avoids sqrt/pow. Assumes RGB channels are bytes/ints 0..255.
        int dr = a.R - b.R;
        int dg = a.G - b.G;
        int db = a.B - b.B;
        // unchecked to avoid overflow checks; values are within safe range here.
        unchecked
        {
            int d2 = dr * dr + dg * dg + db * db;
            return d2 <= maxDistSq;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeColorThresholdSq()
    {
        double backPressure = PaintQoS.BackpressureRatio;
        var minTolerance = 0.01; // always have a tiny tolerance since we really don't need the full RGB space
        var maxTolerance = 0.0225; // at extreme backpressure we can loosen things up, but not to the point of being ugly 
        var lowestBackPressureBeforeIncreasingTolerance = 0f;
        var highestBackPressureBeforeMaxingTolerance = 0.9f;

        // compute tolerance from min to max based on backpressure
        double tolerance;
        if (backPressure <= lowestBackPressureBeforeIncreasingTolerance) tolerance = minTolerance;
        else if (backPressure >= highestBackPressureBeforeMaxingTolerance) tolerance = maxTolerance;
        else
        {
            var range = highestBackPressureBeforeMaxingTolerance - lowestBackPressureBeforeIncreasingTolerance;
            var adj = backPressure - lowestBackPressureBeforeIncreasingTolerance;
            var fract = adj / range;
            tolerance = minTolerance + (fract * (maxTolerance - minTolerance));
        }


        double sq = (tolerance * tolerance) * MaxRgbDistSq;
        // Clamp just in case of FP noise
        if (sq < 0) sq = 0;
        if (sq > MaxRgbDistSq) sq = MaxRgbDistSq;
        return (int)sq;
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

        // e.g., start shedding if the smoothed write cost consistently exceeds SheddingThresholdFactor % of budget
        private static double SheddingThresholdFactor = 0.50;

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
        public static int BudgetedCharsThisFrame()
        {
            var cpm = EstimatedCharsPerMs;
            if (cpm <= 0) return int.MaxValue;

            // shrink headroom as we approach/exceed budget
            double r = BackpressureRatio;
            double headroom = (r <= 0.9) ? 1.12 : (r <= 1.2) ? 1.06 : 1.02;

            var maxChars = cpm * FrameBudgetMs * headroom;
            return (int)Math.Max(256, Math.Min(maxChars, int.MaxValue));
        }

        // How "busy" we are vs the budget. 1.0 == on budget; >1 == over budget.
        public static double BackpressureRatio
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // If we have no samples yet, pretend everything's fine.
                if (_emaWriteMs <= 0 || FrameBudgetMs <= 0) return 0.0;
                return _emaWriteMs / FrameBudgetMs;
            }
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

        private const int Window = 30;
        private static readonly int[] _ring = new int[Window];
        private static int _idx = 0;      // next slot to overwrite
        private static int _count = 0;    // how many valid samples (<= Window)
        private static long _sum = 0;     // running sum (fits comfortably)

        public static int AvgWriteLength => _count == 0 ? 0 : (int)(_sum / _count);

        private static void RecordWriteLength(int length)
        {
            // subtract old value at slot (0 if not yet filled)
            if (_count == Window)
            {
                _sum -= _ring[_idx];
            }
            else
            {
                _count++; // growing phase until full
            }

            _ring[_idx] = length;
            _sum += length;

            _idx++;
            if (_idx == Window) _idx = 0;
        }

   
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
            RecordWriteLength(length);
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
