using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using PosWebApi.Models.Dtos;
using PosWebApi.Tests.Fixtures;

namespace PosWebApi.Tests.Integration.Controllers
{
    /// <summary>
    /// Integration tests for ShiftController: open/close lifecycle, cash drops,
    /// X/Z-Reports, and self-vs-SuperAdmin authorization.
    /// </summary>
    public class ShiftControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientAdmin = null!;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public ShiftControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            _clientUnauthenticated = _factory.GetTestClient();

            var adminClient = _factory.GetTestClient();
            var adminLoginDto = TestDataBuilder.CreateLoginDto().WithUsername("admin").WithPassword("Admin@123456").Build();
            var adminLoginResponse = await adminClient.PostAsJsonAsync("api/auth/login", adminLoginDto);
            adminLoginResponse.EnsureSuccessStatusCode();
            var adminToken = (await adminLoginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken.AccessToken);
            _clientAdmin = adminClient;

            var cashierClient = _factory.GetTestClient();
            var cashierLoginDto = TestDataBuilder.CreateLoginDto().WithUsername("cashier").WithPassword("Cashier@123456").Build();
            var cashierLoginResponse = await cashierClient.PostAsJsonAsync("api/auth/login", cashierLoginDto);
            cashierLoginResponse.EnsureSuccessStatusCode();
            var cashierToken = (await cashierLoginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            cashierClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cashierToken.AccessToken);
            _clientCashier = cashierClient;
        }

        public async Task DisposeAsync()
        {
            _clientAdmin?.Dispose();
            _clientCashier?.Dispose();
            _clientUnauthenticated?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        #region Open Shift Tests

        [Fact]
        public async Task Open_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Open_WithCashierRole_ReturnsCreated()
        {
            var response = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var shift = (await response.Content.ReadAsAsync<ShiftResponseDto>())!;
            shift.RegisterCode.Should().Be("REG-1");
            shift.OpeningFloat.Should().Be(100.00m);
            shift.Status.Should().Be("Open");
        }

        [Fact]
        public async Task Open_WhenAlreadyHasOpenShift_ReturnsConflict()
        {
            await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });

            var response = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-2", OpeningFloat = 50.00m });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Open_WithNegativeOpeningFloat_ReturnsBadRequest()
        {
            var response = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = -10.00m });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Cash Drop Tests

        [Fact]
        public async Task CashDrop_OnOwnOpenShift_ReturnsOk()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            var response = await _clientCashier.PostAsJsonAsync($"api/shifts/{shift.Id}/cash-drop", new CashDropDto { Amount = 30.00m, Note = "midday drop" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CashDrop_OnAnotherCashiersShift_ReturnsForbidden()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            // A second cashier account is needed since the seeded cashier already has an open
            // shift; open registration allows creating another Cashier account here.
            var secondCashierDto = TestDataBuilder.CreateRegisterDto().WithUsername("cashier2").WithEmail("cashier2@example.com").Build();
            await _clientUnauthenticated.PostAsJsonAsync("api/auth/register", secondCashierDto);
            var loginResponse = await _clientUnauthenticated.PostAsJsonAsync("api/auth/login", TestDataBuilder.CreateLoginDto().WithUsername("cashier2").Build());
            var token = (await loginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            var secondClient = _factory.GetTestClient();
            secondClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

            var response = await secondClient.PostAsJsonAsync($"api/shifts/{shift.Id}/cash-drop", new CashDropDto { Amount = 30.00m });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CashDrop_AsSuperAdminOnAnyShift_ReturnsOk()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            var response = await _clientAdmin.PostAsJsonAsync($"api/shifts/{shift.Id}/cash-drop", new CashDropDto { Amount = 30.00m });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region X-Report / Close (Z-Report) Tests

        [Fact]
        public async Task XReport_OnOpenShiftWithNoActivity_ExpectedCashEqualsOpeningFloat()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            var response = await _clientCashier.GetAsync($"api/shifts/{shift.Id}/x-report");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var report = (await response.Content.ReadAsAsync<ShiftReportDto>())!;
            report.IsFinal.Should().BeFalse();
            report.ExpectedCash.Should().Be(100.00m);
        }

        [Fact]
        public async Task Close_WithCountedAmount_ReturnsZReportWithVariance()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            var response = await _clientCashier.PostAsJsonAsync($"api/shifts/{shift.Id}/close", new CloseShiftDto { ClosingCountedAmount = 90.00m });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var report = (await response.Content.ReadAsAsync<ShiftReportDto>())!;
            report.IsFinal.Should().BeTrue();
            report.ClosingCountedAmount.Should().Be(90.00m);
            report.Variance.Should().Be(-10.00m);
        }

        [Fact]
        public async Task Close_AlreadyClosed_ReturnsConflict()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var shift = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;
            await _clientCashier.PostAsJsonAsync($"api/shifts/{shift.Id}/close", new CloseShiftDto { ClosingCountedAmount = 100.00m });

            var response = await _clientCashier.PostAsJsonAsync($"api/shifts/{shift.Id}/close", new CloseShiftDto { ClosingCountedAmount = 100.00m });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        #endregion

        #region GetById / Current Tests

        [Fact]
        public async Task GetById_NotFound_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/shifts/999999");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Current_WithNoOpenShift_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/shifts/current");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Current_WithOpenShift_ReturnsIt()
        {
            var openResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "REG-1", OpeningFloat = 100.00m });
            var opened = (await openResponse.Content.ReadAsAsync<ShiftResponseDto>())!;

            var response = await _clientCashier.GetAsync("api/shifts/current");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var current = (await response.Content.ReadAsAsync<ShiftResponseDto>())!;
            current.Id.Should().Be(opened.Id);
        }

        #endregion
    }
}
