using Xunit.Abstractions;

namespace PosWebApi.Tests
{
    /// <summary>
    /// Helper class for consistent test logging across all test files.
    /// </summary>
    public static class TestLogger
    {
        /// <summary>
        /// Logs a message to the test output.
        /// </summary>
        public static void LogMessage(ITestOutputHelper output, string message)
        {
            output?.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// Logs an error to the test output.
        /// </summary>
        public static void LogError(ITestOutputHelper output, string message, Exception? exception = null)
        {
            var msg = exception == null 
                ? $"[ERROR] {message}"
                : $"[ERROR] {message}: {exception.Message}";
            output?.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {msg}");
        }

        /// <summary>
        /// Logs a warning to the test output.
        /// </summary>
        public static void LogWarning(ITestOutputHelper output, string message)
        {
            output?.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [WARNING] {message}");
        }
    }
}
