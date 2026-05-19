using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class PaintDetailSettings
{
    public const int DefaultDetailPercent = 60;
    public static readonly PaintDetailProfile DefaultProfile = new PaintDetailProfile
    {
        MinTolerance = 0.005,
        MaxTolerance = 0.0225,
        LowestBackpressureBeforeIncreasingTolerance = 0,
        HighestBackpressureBeforeMaxingTolerance = 0.9,
        MaxSoftenedRun = 32,
        FalloffDecay = 0.89,
    };

    public static PaintDetailProfile LowestDetailProfile { get; set; } = new PaintDetailProfile
    {
        MinTolerance = 0.025,
        MaxTolerance = 0.12,
        LowestBackpressureBeforeIncreasingTolerance = 0,
        HighestBackpressureBeforeMaxingTolerance = 0.25,
        MaxSoftenedRun = 64,
        FalloffDecay = 0.985,
    };

    public static PaintDetailProfile HighestDetailProfile { get; set; } = new PaintDetailProfile
    {
        MinTolerance = 0.0015,
        MaxTolerance = 0.012,
        LowestBackpressureBeforeIncreasingTolerance = 0.1,
        HighestBackpressureBeforeMaxingTolerance = 1.2,
        MaxSoftenedRun = 16,
        FalloffDecay = 0.82,
    };

    private static int detailPercent = DefaultDetailPercent;
    private static PaintDetailProfile currentProfile = DefaultProfile;
    public static int DetailPercent
    {
        get => detailPercent;
        set
        {
            detailPercent = Math.Clamp(value, 0, 100);
            currentProfile = ComputeProfile(detailPercent);
        }
    }

    public static PaintDetailProfile CurrentProfile => currentProfile;

    private static PaintDetailProfile ComputeProfile(int percent)
    {
        if (percent == DefaultDetailPercent) return DefaultProfile;

        if (percent < DefaultDetailPercent)
        {
            var t = (DefaultDetailPercent - percent) / (double)DefaultDetailPercent;
            return PaintDetailProfile.Lerp(DefaultProfile, LowestDetailProfile, t);
        }
        else
        {
            var t = (percent - DefaultDetailPercent) / (double)DefaultDetailPercent;
            return PaintDetailProfile.Lerp(DefaultProfile, HighestDetailProfile, t);
        }
    }
}

public sealed class PaintDetailProfile
{
    public double MinTolerance { get; set; }
    public double MaxTolerance { get; set; }
    public double LowestBackpressureBeforeIncreasingTolerance { get; set; }
    public double HighestBackpressureBeforeMaxingTolerance { get; set; }
    public int MaxSoftenedRun { get; set; }
    public double FalloffDecay { get; set; }

    internal static PaintDetailProfile Lerp(PaintDetailProfile from, PaintDetailProfile to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new PaintDetailProfile
        {
            MinTolerance = Lerp(from.MinTolerance, to.MinTolerance, t),
            MaxTolerance = Lerp(from.MaxTolerance, to.MaxTolerance, t),
            LowestBackpressureBeforeIncreasingTolerance = Lerp(from.LowestBackpressureBeforeIncreasingTolerance, to.LowestBackpressureBeforeIncreasingTolerance, t),
            HighestBackpressureBeforeMaxingTolerance = Lerp(from.HighestBackpressureBeforeMaxingTolerance, to.HighestBackpressureBeforeMaxingTolerance, t),
            MaxSoftenedRun = Math.Clamp((int)Math.Round(Lerp(from.MaxSoftenedRun, to.MaxSoftenedRun, t)), 1, PaintCompressor.MaxSoftenedRunCapacity),
            FalloffDecay = Lerp(from.FalloffDecay, to.FalloffDecay, t),
        };
    }

    private static double Lerp(double from, double to, double t) => from + ((to - from) * t);
}

internal static class PaintCompressor
{
    // Precomputed per-frame thresholds (squared) and per-channel caps (sqrt) for offsets 0..MaxSoftenedRun-1
    public const int MaxSoftenedRunCapacity = 64;
    public static readonly int[] s_ThrSqByOffset = new int[MaxSoftenedRunCapacity];
    public static readonly byte[] s_ChannelCapByOffset = new byte[MaxSoftenedRunCapacity];
    public static int MaxSoftenedRun => Math.Clamp(PaintDetailSettings.CurrentProfile.MaxSoftenedRun, 1, MaxSoftenedRunCapacity);

    public static void BuildRunsForLine(ReadOnlySpan<ConsoleCharacter> pixels, int width, int y, int colorThresholdSq, List<Run> runs)
    {
        int row = y * width;
        int x = 0;
        while (x < width)
        {
            ref readonly var first = ref pixels[row + x];
            int start = x;
            bool under = first.IsUnderlined;
            int fgR = first.ForegroundColor.R, fgG = first.ForegroundColor.G, fgB = first.ForegroundColor.B;
            int bgR = first.BackgroundColor.R, bgG = first.BackgroundColor.G, bgB = first.BackgroundColor.B;
            int visibleFgCount = first.Value == ' ' ? 0 : 1;
            int bgCount = 1;
            RGB avgFg = first.ForegroundColor;
            RGB avgBg = first.BackgroundColor;
            x++;

            while (x < width)
            {
                ref readonly var next = ref pixels[row + x];
                if (next.IsUnderlined != under) break;

                int offset = x - start;
                if (CanJoinRun(in next, visibleFgCount > 0, in avgFg, in avgBg, offset, colorThresholdSq) == false) break;

                if (next.Value != ' ')
                {
                    if (visibleFgCount == 0)
                    {
                        fgR = next.ForegroundColor.R;
                        fgG = next.ForegroundColor.G;
                        fgB = next.ForegroundColor.B;
                        visibleFgCount = 1;
                        avgFg = next.ForegroundColor;
                    }
                    else
                    {
                        fgR += next.ForegroundColor.R;
                        fgG += next.ForegroundColor.G;
                        fgB += next.ForegroundColor.B;
                        visibleFgCount++;
                        avgFg = AverageColor(fgR, fgG, fgB, visibleFgCount);
                    }
                }

                bgR += next.BackgroundColor.R;
                bgG += next.BackgroundColor.G;
                bgB += next.BackgroundColor.B;
                bgCount++;
                avgBg = AverageColor(bgR, bgG, bgB, bgCount);
                x++;
            }

            if (visibleFgCount == 0) avgFg = first.ForegroundColor;
            runs.Add(new Run(start, x - start, avgFg, avgBg, under));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IntSqrt(int x)
    {
        // Tiny, branchy integer sqrt for small ranges; good enough for caps (0..~112)
        if (x <= 0) return 0;
        int r = (int)Math.Sqrt(x); // if you want, replace with a branchless int-sqrt; this is fine once per frame*32
        return r;
    }

    public static void BuildPerFrameThresholds(int baseSq)
    {
        for (int i = 0; i < MaxSoftenedRun; i++)
        {
            int m = ComputeToleranceFalloffQ12(i);
            int thrSq = baseSq <= 0 ? 0 : (int)(((long)baseSq * m) >> 12);
            s_ThrSqByOffset[i] = thrSq;

            // per-channel guard: if any |d| > cap then d² > thrSq → early fail
            int cap = IntSqrt(thrSq);
            if (cap > 255) cap = 255;
            s_ChannelCapByOffset[i] = (byte)cap;
        }
    }

    private const int MaxRgbDistSq = 255 * 255 * 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ColorsCloseEnough(in RGB a, in RGB b, int maxDistSq)
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
    public static int ComputeColorThresholdSq()
    {
        double backPressure = PaintQoS.BackpressureRatio;
        var profile = PaintDetailSettings.CurrentProfile;
        var minTolerance = profile.MinTolerance; // always have a tiny tolerance since we really don't need the full RGB space
        var maxTolerance = profile.MaxTolerance; // at extreme backpressure we can loosen things up, but not to the point of being ugly 
        var lowestBackPressureBeforeIncreasingTolerance = profile.LowestBackpressureBeforeIncreasingTolerance;
        var highestBackPressureBeforeMaxingTolerance = profile.HighestBackpressureBeforeMaxingTolerance;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanJoinRun(in ConsoleCharacter c, bool hasVisibleForeground, in RGB avgFg, in RGB avgBg, int offset, int colorThresholdSq)
    {
        if (colorThresholdSq == 0)
        {
            return c.Value == ' '
                ? c.BackgroundColor == avgBg
                : (hasVisibleForeground == false || c.ForegroundColor == avgFg) && c.BackgroundColor == avgBg;
        }

        if (offset >= MaxSoftenedRun)
        {
            return c.Value == ' '
                ? c.BackgroundColor == avgBg
                : (hasVisibleForeground == false || c.ForegroundColor == avgFg) && c.BackgroundColor == avgBg;
        }

        int thrSq = s_ThrSqByOffset[offset];
        byte cap = s_ChannelCapByOffset[offset];

        if (ChannelTooFar(c.BackgroundColor.R, avgBg.R, cap) ||
            ChannelTooFar(c.BackgroundColor.G, avgBg.G, cap) ||
            ChannelTooFar(c.BackgroundColor.B, avgBg.B, cap) ||
            ColorsCloseEnough(c.BackgroundColor, avgBg, thrSq) == false)
        {
            return false;
        }

        if (c.Value == ' ') return true;

        return hasVisibleForeground == false || c.ForegroundColor == avgFg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ChannelTooFar(byte a, byte b, byte cap)
    {
        int d = a - b;
        return (d ^ (d >> 31)) > cap;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static RGB AverageColor(int r, int g, int b, int count)
    {
        return new RGB((byte)((r + (count >> 1)) / count), (byte)((g + (count >> 1)) / count), (byte)((b + (count >> 1)) / count));
    }

    private static int ComputeToleranceFalloffQ12(int offset)
    {
        var decay = PaintDetailSettings.CurrentProfile.FalloffDecay;
        decay = Math.Clamp(decay, 0.01, 1);
        var multiplier = Math.Pow(decay, offset);
        return Math.Clamp((int)Math.Round(multiplier * 4096), 0, 4096);
    }
}
