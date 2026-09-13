using Xunit;

namespace PosWebApi.Tests.Integration.Controllers
{
    /// <summary>
    /// Collection definition for integration tests to share test server instance and database.
    /// </summary>
    [CollectionDefinition("Integration Tests Collection")]
    public class IntegrationTestsCollection : ICollectionFixture<IntegrationTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to define the collection that other test classes belong to.
        // All the magic is in ICollectionFixture<IntegrationTestFixture>
    }

    /// <summary>
    /// Shared fixture for integration tests providing a single test server instance
    /// and shared database context to improve test performance.
    /// </summary>
    public class IntegrationTestFixture : IAsyncLifetime
    {
        private readonly Fixtures.TestWebApplicationFactory _factory;

        public IntegrationTestFixture()
        {
            _factory = new Fixtures.TestWebApplicationFactory();
        }

        public Fixtures.TestWebApplicationFactory Factory => _factory;

        public async Task InitializeAsync()
        {
            // Ensure database is created and seeded
            using (var context = _factory.GetContext())
            {
                await context.Database.EnsureCreatedAsync();
            }
        }

        public async Task DisposeAsync()
        {
            _factory?.Dispose();
            await Task.CompletedTask;
        }
    }
}
