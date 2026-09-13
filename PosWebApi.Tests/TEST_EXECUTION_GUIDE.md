# POS System xUnit Test Suite - Execution Guide

## Overview

This document provides comprehensive instructions for running, analyzing, and maintaining the POS System xUnit test suite (`PosWebApi.Tests`).

## Test Structure

```
PosWebApi.Tests/
├── Fixtures/
│   ├── TestWebApplicationFactory.cs     # In-memory DB configuration, seed data
│   ├── JwtTestHelper.cs                 # JWT token generation and validation
│   └── TestDataBuilder.cs               # Fluent builders for test entities
├── Unit/
│   ├── Services/
│   │   ├── AuthServiceTests.cs          # Auth service unit tests (35+ tests)
│   │   ├── CatalogManagerTests.cs       # Catalog manager unit tests (50+ tests)
│   │   └── PosEngineTests.cs            # POS engine unit tests (45+ tests)
│   └── Helpers/
│       └── TaxHelperTests.cs            # Tax calculation tests (17 tests)
├── Integration/
│   ├── Controllers/
│   │   ├── AuthControllerTests.cs       # Auth endpoint integration tests (28 tests)
│   │   ├── CatalogControllerTests.cs    # Catalog endpoint integration tests (40+ tests)
│   │   └── PosControllerTests.cs        # POS endpoint integration tests (40+ tests)
│   └── IntegrationTestFixture.cs        # Shared fixtures
├── TestConfiguration.cs                 # Global test settings
├── TestLogger.cs                        # Logging helper
└── xunit.runner.json                    # xUnit configuration

Total Test Count: 200+ tests
Coverage Areas: Authentication, Authorization (RBAC), Catalog Management, Cart Operations, Checkout, Tax Calculations
```

## Quick Start

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2026 or VS Code with C# extension
- SQL knowledge (optional, for understanding in-memory DB behavior)

### Running All Tests

```powershell
# Navigate to the POS System directory
cd "C:\Users\Lenovo\source\repos\POS System"

# Run all tests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj

# Run with verbose output
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj -v detailed

# Run with logging
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --logger "console;verbosity=detailed"
```

## Test Execution Strategies

### 1. Run All Tests (Default)

```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj
```

**Output:**
```
Passed:  200
Failed:    0
Skipped:   0
Total:   200
```

### 2. Run Only Unit Tests

```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~Unit"
```

### 3. Run Only Integration Tests

```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~Integration"
```

### 4. Run Specific Test Class

```powershell
# Run only AuthServiceTests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~AuthServiceTests"

# Run only CatalogControllerTests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~CatalogControllerTests"

# Run only PosControllerTests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~PosControllerTests"
```

### 5. Run Specific Test Method

```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~AuthServiceTests.RegisterAsync_WithValidCredentials_CreatesUserSuccessfully"
```

### 6. Run Tests Matching Pattern

```powershell
# Run all registration tests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Register"

# Run all RBAC/authorization tests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Authorization|DisplayName~Role"

# Run all checkout tests
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Checkout"
```

## Code Coverage Analysis

`PosWebApi.Tests.csproj` references `coverlet.collector` (a VSTest data collector), not
`coverlet.msbuild` — so coverage must be collected via `--collect:"XPlat Code Coverage"`.
The `/p:CollectCoverage=true` MSBuild-property style used by `coverlet.msbuild` is **not**
wired up in this project and will silently produce no coverage output.

### Generate Coverage Report

```powershell
# Run tests and collect coverage (writes a per-run coverage.cobertura.xml under a GUID folder)
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### Generate HTML Coverage Report (Recommended)

```powershell
# Install ReportGenerator as global tool (one-time setup)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report from the cobertura file dotnet test just produced
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" `
  -targetdir:"PosWebApi.Tests/CoverageReport" `
  -reporttypes:"HtmlInline_Dark;Cobertura"
```

Then open `PosWebApi.Tests/CoverageReport/index.html` in a browser to view the detailed coverage report.

## Performance Optimization

### Parallel Test Execution

Tests are configured to run in parallel by default (see `xunit.runner.json`). To customize:

```powershell
# Run tests sequentially (slower but useful for debugging)
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj -- --maxParallelThreads=1

# Run with specific thread count
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj -- --maxParallelThreads=4
```

### Faster Test Runs

```powershell
# Skip long-running tests (e.g., password hashing)
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "Category!=Slow"

# Run only tests matching a category
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "Category=Fast"
```

## Continuous Integration (CI/CD)

### GitHub Actions / Azure Pipelines

```yaml
# Example CI/CD configuration
- name: Run xUnit Tests
  run: dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --no-build --configuration Release

- name: Generate Coverage Report
  run: |
	dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --no-build --collect:"XPlat Code Coverage" --results-directory ./coverage
	reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:"coverage-report"

- name: Upload Coverage
  uses: codecov/codecov-action@v3
  with:
	files: ./coverage/**/coverage.cobertura.xml
```

## Test Categories

### 1. Authentication Tests (28 tests)

**Location:** `PosWebApi.Tests/Integration/Controllers/AuthControllerTests.cs`

Tests cover:
- User registration with validation
- Login with JWT token generation
- Profile endpoint access
- Token validation
- Logout functionality
- Role-based registration restrictions

**Run Command:**
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~AuthControllerTests"
```

### 2. Catalog Management Tests (90+ tests)

**Locations:** 
- Unit: `PosWebApi.Tests/Unit/Services/CatalogManagerTests.cs` (50+ tests)
- Integration: `PosWebApi.Tests/Integration/Controllers/CatalogControllerTests.cs` (40+ tests)

Tests cover:
- Product CRUD operations
- SKU uniqueness and case-insensitivity
- Stock management and deduction
- Low stock queries
- RBAC authorization (SuperAdmin-only operations)
- Price validation
- Inventory precision

**Run Command:**
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Catalog"
```

### 3. POS / Cart / Checkout Tests (85+ tests)

**Locations:**
- Unit: `PosWebApi.Tests/Unit/Services/PosEngineTests.cs` (45+ tests)
- Integration: `PosWebApi.Tests/Integration/Controllers/PosControllerTests.cs` (40+ tests)

Tests cover:
- Cart add/undo/clear operations
- Checkout with order persistence
- Tax and total calculations
- Stock deduction on checkout
- Multiple item purchases
- Edge cases (empty cart, insufficient stock)
- Real-world transaction flows

**Run Command:**
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Checkout|DisplayName~Cart"
```

### 4. Tax Calculation Tests (17 tests)

**Location:** `PosWebApi.Tests/Unit/Helpers/TaxHelperTests.cs`

Tests cover:
- Default 15% tax rate
- Custom tax rates
- Decimal precision
- Edge cases (zero, negative, max values)

**Run Command:**
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "FullyQualifiedName~TaxHelperTests"
```

### 5. Authorization / RBAC Tests (70+ tests)

Tests across all integration controllers verify:
- SuperAdmin-only endpoints (403 Forbidden for Cashier)
- Cashier-accessible endpoints (200 OK)
- Unauthenticated access (401 Unauthorized)

**Run Command:**
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --filter "DisplayName~Authorization|DisplayName~Forbidden|DisplayName~Unauthorized"
```

## Debugging Failed Tests

### Visual Studio IDE

1. Open `Test Explorer` (Test → Windows → Test Explorer)
2. Select failed test
3. Right-click → Debug Selected Tests
4. Step through code using breakpoints

### Command Line Debugging

```powershell
# Run specific test with detailed output
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj `
  --filter "FullyQualifiedName~AuthServiceTests.LoginAsync_WithValidCredentials_ReturnsTokenResponseWithJwt" `
  --logger "console;verbosity=detailed" `
  --verbosity diagnostic
```

### Enable Test Output

```csharp
// In any test class, inject ITestOutputHelper
public class MyTest
{
	private readonly ITestOutputHelper _output;

	public MyTest(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void MyTest()
	{
		_output.WriteLine("Debug message here");
	}
}
```

Then run with:
```powershell
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --logger "console;verbosity=detailed"
```

## Test Maintenance

### Adding New Tests

1. Create new test class in appropriate folder (Unit/Services or Integration/Controllers)
2. Implement `IAsyncLifetime` for async setup/teardown
3. Use `TestDataBuilder` for entity creation
4. Follow AAA pattern (Arrange-Act-Assert)
5. Use `FluentAssertions` for readable assertions

### Example Test Template

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
	// Arrange
	var entity = TestDataBuilder.CreateUser().WithUsername("test").Build();

	// Act
	var result = await service.SomeMethod(entity);

	// Assert
	result.Should().NotBeNull();
	result.Property.Should().Be(expectedValue);
}
```

### Updating Fixtures

- **TestWebApplicationFactory**: Modify seed data in `SeedTestData()` method
- **JwtTestHelper**: Add new token generation methods
- **TestDataBuilder**: Add new builders for new entity types

## Performance Benchmarks

**Expected Test Execution Times:**

| Test Category | Count | Time | Notes |
|---|---|---|---|
| Auth Service (Unit) | 35 | ~5 sec | Password hashing is slow (BCrypt) |
| Catalog Manager (Unit) | 50 | ~2 sec | In-memory DB is fast |
| POS Engine (Unit) | 45 | ~3 sec | Cart operations are simple |
| Tax Helper (Unit) | 17 | <1 sec | Arithmetic only |
| Auth Controller (Integration) | 28 | ~4 sec | HTTP round-trips |
| Catalog Controller (Integration) | 40 | ~6 sec | DB seed + HTTP |
| POS Controller (Integration) | 40 | ~8 sec | Complex checkout logic |
| **TOTAL** | **200+** | **~30 sec** | **Parallel execution** |

## Troubleshooting

### Issue: Tests timeout
**Solution:** Increase timeout in TestConfiguration.cs or run tests sequentially

```powershell
dotnet test -- --maxParallelThreads=1
```

### Issue: "Database is locked" errors
**Solution:** This shouldn't happen with in-memory DB. If it does, ensure proper IAsyncLifetime usage.

### Issue: JWT token validation failures
**Solution:** Verify secret matches in JwtTestHelper and appsettings.json

### Issue: Role claims missing in tokens
**Solution:** Check that AuthService.GenerateJwtToken includes ClaimTypes.Role

## Best Practices

1. **Use Test Builders**: Always use TestDataBuilder for entity creation
2. **Isolate Tests**: Each test should be independent; use fresh in-memory DB instances
3. **Mock Judiciously**: Only mock external dependencies, not the class under test
4. **Name Tests Clearly**: Use "Method_Scenario_Result" naming convention
5. **Keep Tests Fast**: Avoid unnecessary waits or complex setup
6. **Document Complex Tests**: Use inline comments for non-obvious test logic
7. **Review Coverage**: Aim for >80% code coverage for core business logic

## CI/CD Integration

### Pre-Commit Hook

Create `.git/hooks/pre-commit`:
```bash
#!/bin/bash
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --no-build
if [ $? -ne 0 ]; then
	echo "Tests failed. Commit aborted."
	exit 1
fi
```

### Pre-Push Hook

Create `.git/hooks/pre-push`:
```bash
#!/bin/bash
dotnet test PosWebApi.Tests/PosWebApi.Tests.csproj --no-build --configuration Release --collect:"XPlat Code Coverage"
if [ $? -ne 0 ]; then
	echo "Tests failed. Push aborted."
	exit 1
fi
```

## Summary

This test suite provides comprehensive coverage of the POS System API with 200+ tests across unit and integration layers. Tests are designed to:

- ✅ Verify business logic correctness
- ✅ Enforce RBAC authorization
- ✅ Validate JWT authentication
- ✅ Test edge cases and error scenarios
- ✅ Support continuous integration
- ✅ Enable confident refactoring

For questions or issues, refer to the inline test documentation and use the debugging techniques outlined above.
