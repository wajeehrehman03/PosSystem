using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PosWebApi.Data;
using PosWebApi.Models;
using PosWebApi.Services;

namespace PosWebApi.Tests.Fixtures
{
    /// <summary>
    /// Custom WebApplicationFactory for testing that configures in-memory database and test dependencies.
    /// </summary>
    public class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName;

        public TestWebApplicationFactory(string? dbName = null)
        {
            _dbName = dbName ?? $"TestDb_{Guid.NewGuid()}";
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Program.cs requires Jwt:Secret/Issuer/Audience at startup; the test host's
                // content root doesn't pick up the main project's appsettings.json, so supply
                // them directly. Values match JwtTestHelper so hand-built test tokens validate.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "TestOnlyNotForProductionSecretKey32BytesLong",
                    ["Jwt:Issuer"] = "PosSystemApi",
                    ["Jwt:Audience"] = "PosSystemClient",
                    ["Jwt:ExpiryMinutes"] = "15"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove every production database service touching AppDbContext. Program.cs
                // registers it via AddDbContextPool (not plain AddDbContext), which adds several
                // extra singleton pooling-infrastructure descriptors (IDbContextPool<AppDbContext>,
                // IScopedDbContextLease<AppDbContext>, etc.) beyond just DbContextOptions<AppDbContext>
                // and AppDbContext itself - removing only those two left an orphaned singleton pool
                // still wired to the old (now-scoped, post-override) options, which the container's
                // startup validation correctly rejected as an invalid scoped-from-singleton capture.
                // Sweeping every descriptor generic over AppDbContext (or exactly AppDbContext)
                // clears all of it before re-adding fresh via plain AddDbContext below.
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(AppDbContext)
                        || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
                    .ToList();
                foreach (var d in descriptorsToRemove)
                {
                    services.Remove(d);
                }

                // Add in-memory database for testing. PosEngine.Checkout and
                // CatalogManager.DeductStock wrap their saves in a DB transaction (required
                // against the real MySQL provider); the in-memory provider doesn't support
                // transactions and treats using one as a warning-level error by default.
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });

                // Replace the real Redis-backed IDistributedCache (Program.cs) with an in-memory
                // one - CatalogManager's cached reads are exercised by integration tests over
                // HTTP, and those must not require a live Redis server to run.
                var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
                if (cacheDescriptor != null)
                {
                    services.Remove(cacheDescriptor);
                }
                services.AddDistributedMemoryCache();

                // Build service provider for database initialization
                using (var serviceProvider = services.BuildServiceProvider())
                {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        context.Database.EnsureCreated();
                        SeedTestData(context);
                    }
                }
            });

            base.ConfigureWebHost(builder);
        }

        /// <summary>
        /// Seeds initial test data into the in-memory database.
        /// </summary>
        private static void SeedTestData(AppDbContext context)
        {
            // Clear existing data
            context.Users.RemoveRange(context.Users);
            context.Products.RemoveRange(context.Products);
            context.Orders.RemoveRange(context.Orders);
            context.CartItems.RemoveRange(context.CartItems);
            context.Registers.RemoveRange(context.Registers);
            context.SaveChanges();

            // Seed default users (as configured in AppDbContext)
            var adminUser = new User
            {
                Id = 1,
                Username = "admin",
                Email = "admin@possystem.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                Role = UserRoleConstants.SuperAdmin,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                LastLoginAt = null
            };

            var cashierUser = new User
            {
                Id = 2,
                Username = "cashier",
                Email = "cashier@possystem.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Cashier@123456"),
                Role = UserRoleConstants.Cashier,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                LastLoginAt = null
            };

            context.Users.AddRange(adminUser, cashierUser);

            // Seed test products
            var products = new[]
            {
                new Product { Id = 1, Sku = "SKU001", Name = "Product 1", Price = 10.00m, StockQuantity = 100, MinimumStockThreshold = 5 },
                new Product { Id = 2, Sku = "SKU002", Name = "Product 2", Price = 20.00m, StockQuantity = 50, MinimumStockThreshold = 5 },
                new Product { Id = 3, Sku = "SKU003", Name = "Product 3", Price = 15.50m, StockQuantity = 2, MinimumStockThreshold = 5 },
                new Product { Id = 4, Sku = "SKU004", Name = "Product 4", Price = 25.00m, StockQuantity = 0, MinimumStockThreshold = 10 }
            };

            context.Products.AddRange(products);

            // Every register code referenced by an OpenShiftDto across the integration test
            // suite (opening a shift now validates the code against a real, active Register -
            // see ShiftService.OpenShift), seeded once here rather than per-test.
            var registers = new[]
            {
                new Register { Code = "REG-1", Name = "Register 1", IsActive = true },
                new Register { Code = "REG-2", Name = "Register 2", IsActive = true },
                new Register { Code = "TEST-REGISTER", Name = "Test Register", IsActive = true },
                new Register { Code = "SYNC-TEST-REGISTER", Name = "Sync Test Register", IsActive = true }
            };
            context.Registers.AddRange(registers);

            context.SaveChanges();
        }

        /// <summary>
        /// Gets or creates a test HTTP client configured for the test server.
        /// </summary>
        public HttpClient GetTestClient()
        {
            return CreateClient();
        }

        /// <summary>
        /// Gets the test database context for direct entity operations.
        /// </summary>
        public AppDbContext GetContext()
        {
            var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }
    }
}
