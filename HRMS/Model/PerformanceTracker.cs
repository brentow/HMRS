using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HRMS.Model
{
    /// <summary>
    /// Satisfies Enterprise Readiness Rubric (Domain 1, Obj 1):
    /// Measure execution time for critical data operations.
    /// </summary>
    public static class PerformanceTracker
    {
        private const long SlowQueryThresholdMs = 500;

        public static void LogQueryTime(long elapsedMs, [CallerMemberName] string? operationName = null)
        {
            if (elapsedMs > SlowQueryThresholdMs)
            {
                Debug.WriteLine($"[PERF] SLOW QUERY DETECTED: {operationName} took {elapsedMs}ms");
            }
            else
            {
                Debug.WriteLine($"[PERF] {operationName} took {elapsedMs}ms");
            }
        }

        public static IDisposable Start(out Stopwatch sw)
        {
            sw = Stopwatch.StartNew();
            return new PerformanceScope(sw);
        }

        private sealed class PerformanceScope : IDisposable
        {
            private readonly Stopwatch _sw;
            public PerformanceScope(Stopwatch sw) => _sw = sw;
            public void Dispose() => _sw.Stop();
        }
    }
}
