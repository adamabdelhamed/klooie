using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace klooie
{
    // Base class for tracking stack traces
    public class StackHunter
    {
        // Dictionary to track counts of stack traces
        private readonly Dictionary<ComparableStackTrace, int> _stackTraces = new Dictionary<ComparableStackTrace, int>();
        
        public List<KeyValuePair<ComparableStackTrace,int>> SortedTraces => _stackTraces.OrderByDescending(x => x.Value).ToList();
        

        /// <summary>
        /// Registers the current stack trace, skipping and taking a specified number of frames.
        /// The stack trace is only recorded if it isn't an exception.
        /// </summary>
        public void RegisterCurrentStackTrace(int skip, int take)
        {
            var trace = new StackTrace(true);
            var frames = trace.GetFrames();
            if (frames == null) return;

            // Skip the first few frames (e.g., the hunter's methods) and take the ones we care about.
            frames = frames.Skip(skip).Take(take).ToArray();
            var comparableTrace = new ComparableStackTrace(frames);

            // Check if this stack trace should be excluded
            if (!ShouldRecordStackTrace(comparableTrace))
                return;

            // Record or update count
            if (_stackTraces.TryGetValue(comparableTrace, out var count))
            {
                _stackTraces[comparableTrace] = count + 1;
            }
            else
            {
                _stackTraces.Add(comparableTrace, 1);
            }
        }

        /// <summary>
        /// Determines whether the provided stack trace should be recorded.
        /// Derived classes can override this method to filter out known exceptions.
        /// </summary>
        protected virtual bool ShouldRecordStackTrace(ComparableStackTrace stackTrace)
        {
            return true;
        }

     

        /// <summary>
        /// Gets a snapshot of the tracked stack traces.
        /// </summary>
        public IReadOnlyDictionary<ComparableStackTrace, int> StackTraces => _stackTraces;
    }

    /// <summary>
    /// A wrapper for StackFrame[] that compares equality based on the methods in the frames.
    /// </summary>
    public class ComparableStackTrace : IEquatable<ComparableStackTrace>
    {
        public StackFrame[] Frames { get; init; }

        public ComparableStackTrace(StackFrame[] frames)
        {
            Frames = frames;
        }

        public bool Equals(ComparableStackTrace? other)
        {
            if (other == null) return false;
            if (Frames.Length != other.Frames.Length) return false;

            for (int i = 0; i < Frames.Length; i++)
            {
                var frame1 = Frames[i];
                var frame2 = other.Frames[i];
                var method1 = frame1.GetMethod();
                var method2 = frame2.GetMethod();

                // Compare method info first
                if (method1 != method2) return false;

                // Then compare line numbers to differentiate same-method calls
                // Note: GetFileLineNumber may return 0 if debug symbols are not available
                if (frame1.GetFileLineNumber() != frame2.GetFileLineNumber()) return false;
            }
            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ComparableStackTrace);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var frame in Frames)
            {
                var method = frame.GetMethod();
                // Incorporate method hash code and line number
                hash = hash * 31 + (method?.GetHashCode() ?? 0);
                hash = hash * 31 + frame.GetFileLineNumber();
            }
            return hash;
        }

        public string DumpStackTraceAsExceptionSnippet()
        {
            var sb = new StringBuilder();
            sb.AppendLine("new ComparableStackTrace(new StackFrame[]");
            sb.AppendLine("{");

            foreach (var frame in Frames)
            {
                var method = frame.GetMethod();
                var file = frame.GetFileName();
                var line = frame.GetFileLineNumber();

                if (method == null)
                    continue;

                var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
                file = file?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "null";

                sb.AppendLine($"    new StackFrame(\"{file}\", {line}), // {methodName}");
            }

            sb.AppendLine("}),");
            return sb.ToString();
        }

        public bool EndsWith(ComparableStackTrace other)
        {
            // The exception trace must not be longer than the captured trace.
            if (other.Frames.Length > this.Frames.Length)
                return false;

            // Compare each frame from the tail of the captured trace with the corresponding frame in the exception.
            for (int i = 0; i < other.Frames.Length; i++)
            {
                var myFrame = this.Frames[i];
                var otherFrame = other.Frames[i];

                if (!FrameMatches(myFrame, otherFrame))
                    return false;
            }

            return true;
        }

        private bool FrameMatches(StackFrame frame1, StackFrame frame2)
        {
            // Try comparing via method information first.

            // Fallback to comparing file name and line number.
            var file1 = frame1.GetFileName() ?? "";
            var file2 = frame2.GetFileName() ?? "";
            return string.Equals(file1, file2, StringComparison.OrdinalIgnoreCase) &&
                    frame1.GetFileLineNumber() == frame2.GetFileLineNumber();
            
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var frame in Frames)
            {
                var method = frame.GetMethod();
                var lineNumber = frame.GetFileLineNumber();
                if (method != null)
                    sb.AppendLine($"{method.DeclaringType?.Name}.{method.Name} (Line: {lineNumber})");
            }
            return sb.ToString();
        }
    }

}
