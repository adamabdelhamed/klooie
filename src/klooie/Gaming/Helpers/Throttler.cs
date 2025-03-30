using System.Diagnostics;

namespace klooie.Gaming
{
    /// <summary>
    /// A rate governor that limits the number of invocations within a time window.
    /// Internally, it uses Stopwatch.GetTimestamp for precise time measurement.
    /// </summary>
    public sealed class Throttler
    {
        // Queue to hold the high-resolution timestamps (Stopwatch ticks) of successful firings.
        private readonly Queue<long> _fireTimestamps = new Queue<long>();

        // Stores the last firing timestamp.
        private long _lastFireTimestamp;

        /// <summary>
        /// The maximum allowed invocations in the given time window.
        /// </summary>
        public int MaxInvocations { get; }

        /// <summary>
        /// The time window in which at most MaxInvocations are allowed.
        /// </summary>
        public TimeSpan TimeWindow { get; }

        // Conversion factor from Stopwatch ticks to seconds.
        private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;

#if DEBUG
        // Debug counters to track attempts and successful firings.
        private long _attemptCount;
        private long _successCount;

        /// <summary>
        /// Total number of calls made to ShouldFire (attempts).
        /// </summary>
        public long TotalAttempts => _attemptCount;

        /// <summary>
        /// Total number of successful firings.
        /// </summary>
        public long TotalSuccesses => _successCount;

        /// <summary>
        /// Gets the ratio of attempts per successful firing.
        /// </summary>
        public double AttemptsPerSuccess => _successCount == 0 ? 0 : (double)_attemptCount / _successCount;
#endif

        /// <summary>
        /// Creates a new rate governor.
        /// </summary>
        /// <param name="maxInvocations">The maximum number of allowed invocations in the given time window.</param>
        /// <param name="timeWindow">The duration of the time window.</param>
        public Throttler(int maxInvocations, TimeSpan timeWindow)
        {
            if (maxInvocations <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxInvocations), "Must be greater than zero.");
            if (timeWindow <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeWindow), "Must be greater than zero.");

            MaxInvocations = maxInvocations;
            TimeWindow = timeWindow;
            _lastFireTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Determines if a firing is allowed at the current time.
        /// Uses Stopwatch for high precision timing.
        /// </summary>
        /// <returns>
        /// True if there have been fewer than MaxInvocations in the past TimeWindow; otherwise, false.
        /// </returns>
        public bool ShouldFire()
        {
#if DEBUG
            _attemptCount++;
#endif
            long currentTimestamp = Stopwatch.GetTimestamp();

            // Purge timestamps that fall outside the time window.
            while (_fireTimestamps.Count > 0 &&
                   GetElapsedTime(_fireTimestamps.Peek(), currentTimestamp) > TimeWindow)
            {
                _fireTimestamps.Dequeue();
            }

            if (_fireTimestamps.Count < MaxInvocations)
            {
                _fireTimestamps.Enqueue(currentTimestamp);
                _lastFireTimestamp = currentTimestamp;
#if DEBUG
                _successCount++;
#endif
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets the governor by clearing the history of firings and treating the current time as the last firing time.
        /// </summary>
        public void Reset()
        {
            long currentTimestamp = Stopwatch.GetTimestamp();
            _fireTimestamps.Clear();
            _fireTimestamps.Enqueue(currentTimestamp);
            _lastFireTimestamp = currentTimestamp;
#if DEBUG
            // Optionally, reset debug counters if desired.
            _attemptCount = 0;
            _successCount = 0;
#endif
        }

        /// <summary>
        /// Computes the elapsed time between two Stopwatch timestamps.
        /// </summary>
        /// <param name="startTimestamp">The starting timestamp.</param>
        /// <param name="endTimestamp">The ending timestamp.</param>
        /// <returns>The elapsed time as a TimeSpan.</returns>
        private static TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp)
        {
            long ticksElapsed = endTimestamp - startTimestamp;
            double seconds = ticksElapsed * TicksToSeconds;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
