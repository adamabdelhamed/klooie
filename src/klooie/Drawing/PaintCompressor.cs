using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
internal static class PaintCompressor
{

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
    public static readonly int[] s_ThrSqByOffset = new int[MaxSoftenedRun];
    public static readonly byte[] s_ChannelCapByOffset = new byte[MaxSoftenedRun];

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
    public const int MaxSoftenedRun = 32;

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
}
