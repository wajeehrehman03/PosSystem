using Xunit;

namespace PosWebApi.Tests
{
    /// <summary>
    /// Custom test output helper and configuration for organizing test execution.
    /// </summary>
    public class TestConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether to run slow tests (e.g., actual password hashing with BCrypt).
        /// Set to false to speed up unit test runs.
        /// </summary>
        public static bool RunSlowTests { get; set; } = true;

        /// <summary>
        /// Gets or sets the default timeout for integration tests (in milliseconds).
        /// </summary>
        public static int IntegrationTestTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the default timeout for unit tests (in milliseconds).
        /// </summary>
        public static int UnitTestTimeout { get; set; } = 5000;
    }
}
