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
    /// Integration tests for DiscountCodesController management endpoints and RBAC.
    /// </summary>
    public class DiscountCodesControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientAdmin = null!;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public DiscountCodesControllerTests()
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

        private static CreateDiscountCodeDto SampleCode(string code = "SAVE10") => new CreateDiscountCodeDto
        {
            Code = code,
            Type = DiscountCodeTypeConstants.Percent,
            Value = 10m
        };

        [Fact]
        public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/discount-codes", SampleCode());

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Create_WithCashierRole_ReturnsForbidden()
        {
            var response = await _clientCashier.PostAsJsonAsync("api/discount-codes", SampleCode());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Create_WithAdminRole_ReturnsCreated()
        {
            var response = await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("NEWCODE1"));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var dto = await response.Content.ReadAsAsync<DiscountCodeResponseDto>();
            dto!.Code.Should().Be("NEWCODE1");
            dto.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Create_WithDuplicateCode_ReturnsConflict()
        {
            await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("DUPE1"));

            var response = await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("dupe1"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Create_WithPercentOver100_ReturnsBadRequest()
        {
            var request = new CreateDiscountCodeDto { Code = "TOOBIG", Type = DiscountCodeTypeConstants.Percent, Value = 150m };

            var response = await _clientAdmin.PostAsJsonAsync("api/discount-codes", request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetAll_WithCashierRole_ReturnsForbidden()
        {
            var response = await _clientCashier.GetAsync("api/discount-codes");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetAll_WithAdminRole_ReturnsOk()
        {
            await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("LISTME"));

            var response = await _clientAdmin.GetAsync("api/discount-codes");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetByCode_WithUnknownCode_ReturnsNotFound()
        {
            var response = await _clientAdmin.GetAsync("api/discount-codes/DOESNOTEXIST");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Deactivate_WithActiveCode_Succeeds()
        {
            await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("DEACT1"));

            var response = await _clientAdmin.PatchAsync("api/discount-codes/DEACT1/deactivate", null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadAsAsync<DiscountCodeResponseDto>();
            dto!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task Deactivate_AlreadyInactive_ReturnsConflict()
        {
            await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("DEACT2"));
            await _clientAdmin.PatchAsync("api/discount-codes/DEACT2/deactivate", null);

            var response = await _clientAdmin.PatchAsync("api/discount-codes/DEACT2/deactivate", null);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Deactivate_WithUnknownCode_ReturnsNotFound()
        {
            var response = await _clientAdmin.PatchAsync("api/discount-codes/DOESNOTEXIST/deactivate", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Deactivate_WithCashierRole_ReturnsForbidden()
        {
            await _clientAdmin.PostAsJsonAsync("api/discount-codes", SampleCode("DEACT3"));

            var response = await _clientCashier.PatchAsync("api/discount-codes/DEACT3/deactivate", null);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
