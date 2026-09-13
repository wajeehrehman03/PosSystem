using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Tests.Fixtures;

namespace PosWebApi.Tests.Integration.Controllers
{
    /// <summary>
    /// Integration tests for CustomersController loyalty customer registration/lookup.
    /// </summary>
    public class CustomersControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientAdmin = null!;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public CustomersControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            _clientUnauthenticated = _factory.GetTestClient();

            var adminClient = _factory.GetTestClient();
            var adminLoginResponse = await adminClient.PostAsJsonAsync("api/auth/login", TestDataBuilder.CreateLoginDto().WithUsername("admin").WithPassword("Admin@123456").Build());
            adminLoginResponse.EnsureSuccessStatusCode();
            var adminToken = (await adminLoginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken.AccessToken);
            _clientAdmin = adminClient;

            var cashierClient = _factory.GetTestClient();
            var cashierLoginResponse = await cashierClient.PostAsJsonAsync("api/auth/login", TestDataBuilder.CreateLoginDto().WithUsername("cashier").WithPassword("Cashier@123456").Build());
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

        [Fact]
        public async Task Register_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "555-111-2222", Name = "Jane Doe" });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Register_WithCashierRole_Succeeds()
        {
            var response = await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "555-111-2222", Name = "Jane Doe" });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var customer = await response.Content.ReadAsAsync<CustomerResponseDto>();
            customer!.Phone.Should().Be("5551112222");
            customer.Name.Should().Be("Jane Doe");
            customer.LoyaltyPoints.Should().Be(0);
            customer.Tier.Should().Be(CustomerTierConstants.Standard);
        }

        [Fact]
        public async Task Register_WithDuplicatePhone_ReturnsConflict()
        {
            await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "555-111-3333", Name = "First" });

            // Different formatting, same normalized number
            var response = await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "(555) 111-3333", Name = "Second" });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Register_WithMissingName_ReturnsBadRequest()
        {
            var response = await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "5551114444", Name = "" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetByPhone_WithRegisteredCustomer_ReturnsOk()
        {
            await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "5551115555", Name = "Found Me" });

            var response = await _clientCashier.GetAsync("api/customers/5551115555");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var customer = await response.Content.ReadAsAsync<CustomerResponseDto>();
            customer!.Name.Should().Be("Found Me");
        }

        [Fact]
        public async Task GetByPhone_WithUnknownPhone_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/customers/5559998888");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetLoyaltyHistory_WithUnknownPhone_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/customers/5559998888/loyalty-history");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetLoyaltyHistory_WithNoTransactionsYet_ReturnsEmptyList()
        {
            await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "5551116666", Name = "No History" });

            var response = await _clientCashier.GetAsync("api/customers/5551116666/loyalty-history");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
