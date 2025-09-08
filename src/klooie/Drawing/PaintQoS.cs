using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal static class PaintQoS
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