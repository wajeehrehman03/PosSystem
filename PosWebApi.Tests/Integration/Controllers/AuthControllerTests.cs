using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using PosWebApi.Models.Dtos;
using PosWebApi.Tests.Fixtures;

namespace PosWebApi.Tests.Integration.Controllers
{
    /// <summary>
    /// Integration tests for AuthController endpoints with full HTTP stack and authorization.
    /// </summary>
    public class AuthControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _client = null!;

        public AuthControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            _client = _factory.GetTestClient();
            // Ensure client is ready
            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        #region Registration Tests

        [Fact]
        public async Task Register_WithValidCredentials_ReturnsCreatedStatusWithUserData()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("newuser")
                .WithEmail("newuser@example.com")
                .WithPassword("SecurePassword123")
                .WithConfirmPassword("SecurePassword123")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((object)content!).Should().NotBeNull();
            ((string)content!.user.username).Should().Be("newuser");
            ((string)content!.user.role).Should().Be("Cashier");
        }

        [Fact]
        public async Task Register_WithDuplicateUsername_ReturnsConflict()
        {
            // Arrange - "admin" already exists from seed data
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("admin")
                .WithEmail("different@example.com")
                .WithPassword("Password123")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Register_WithMismatchedPasswords_ReturnsBadRequest()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("testuser")
                .WithPassword("Password123")
                .WithConfirmPassword("DifferentPassword456")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("do not match");
        }

        [Fact]
        public async Task Register_WithShortPassword_ReturnsBadRequest()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("testuser")
                .WithPassword("short")
                .WithConfirmPassword("short")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithMissingUsername_ReturnsBadRequest()
        {
            // Arrange
            var registerDto = new { email = "test@example.com", password = "Password123", confirmPassword = "Password123" };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithNullBody_ReturnsBadRequest()
        {
            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", (string?)null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_WithoutAuthenticationNonSuperAdminAttemptingSuperAdmin_ReturnsForbidden()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("newadmin")
                .WithEmail("newadmin@example.com")
                .AsSuperAdmin()
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Register_SuperAdminCreatingAnotherSuperAdmin_SucceedsWhenAuthenticated()
        {
            // Arrange
            var adminLoginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", adminLoginDto);
            loginResponse.EnsureSuccessStatusCode();
            var loginContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();

            var newSuperAdminDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("newsuperadmin")
                .WithEmail("newsuperadmin@example.com")
                .AsSuperAdmin()
                .Build();

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginContent!.AccessToken);

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", newSuperAdminDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((string)content!.user.role).Should().Be("SuperAdmin");
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsOkWithJwtToken()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", loginDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<TokenResponseDto>();
            content.Should().NotBeNull();
            content!.AccessToken.Should().NotBeNullOrWhiteSpace();
            content.TokenType.Should().Be("Bearer");
            content.ExpiresIn.Should().BeGreaterThan(0);
            content.User.Should().NotBeNull();
            content.User!.Username.Should().Be("admin");
            content.User.Role.Should().Be("SuperAdmin");
        }

        [Fact]
        public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("WrongPassword123")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", loginDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("nonexistent")
                .WithPassword("Password123")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", loginDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_WithNullBody_ReturnsBadRequest()
        {
            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", (string?)null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_GeneratedTokenContainsRoleClaim()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("cashier")
                .WithPassword("Cashier@123456")
                .Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", loginDto);
            var content = await response.Content.ReadAsAsync<TokenResponseDto>();

            // Assert
            var claims = JwtTestHelper.ExtractClaims(content!.AccessToken);
            claims.Should().ContainSingle(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Cashier");
        }

        #endregion

        #region Profile Tests

        [Fact]
        public async Task GetProfile_WithValidToken_ReturnsUserInfo()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", loginDto);
            var tokenContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenContent!.AccessToken);

            // Act
            var response = await _client.GetAsync("api/auth/profile");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<UserResponseDto>();
            content.Should().NotBeNull();
            content!.Username.Should().Be("admin");
            content.Role.Should().Be("SuperAdmin");
        }

        [Fact]
        public async Task GetProfile_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("api/auth/profile");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetProfile_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

            // Act
            var response = await _client.GetAsync("api/auth/profile");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Token Validation Tests

        [Fact]
        public async Task ValidateToken_WithValidToken_ReturnsOk()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", loginDto);
            var tokenContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();

            var validateRequest = new { token = tokenContent!.AccessToken };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/validate-token", validateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ValidateToken_WithInvalidToken_ReturnsOkWithIsValidFalse()
        {
            // Arrange
            var validateRequest = new { token = "invalid.token.here" };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/validate-token", validateRequest);

            // Assert - AuthController.ValidateToken only returns BadRequest for a missing token;
            // a syntactically-present but invalid token still gets a 200 with isValid: false.
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((bool)content!.isValid).Should().BeFalse();
        }

        [Fact]
        public async Task ValidateToken_WithoutAuthentication_Succeeds()
        {
            // Note: ValidateToken should be public to allow pre-flight validation
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", loginDto);
            var tokenContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();

            var validateRequest = new { token = tokenContent!.AccessToken };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/validate-token", validateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Logout Tests

        [Fact]
        public async Task Logout_WithValidToken_ReturnsOk()
        {
            // Arrange
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", loginDto);
            var tokenContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenContent!.AccessToken);

            // Act
            var response = await _client.PostAsync("api/auth/logout", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Logout_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.PostAsync("api/auth/logout", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Endpoint Accessibility Tests

        [Fact]
        public async Task Login_IsPublicEndpoint_AccessibleWithoutAuthentication()
        {
            // Arrange
            var loginDto = new { username = "admin", password = "Admin@123456" };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/login", loginDto);

            // Assert
            response.Should().NotBeNull();
            // Should not get 401 Unauthorized
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Register_IsPublicEndpoint_AccessibleWithoutAuthentication()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto().Build();

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            response.Should().NotBeNull();
            // Should not get 401 Unauthorized for the endpoint itself
            // (though it may fail for business logic reasons)
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ValidateToken_IsPublicEndpoint_AccessibleWithoutAuthentication()
        {
            // Arrange
            var validateRequest = new { token = "some.token.here" };

            // Act
            var response = await _client.PostAsJsonAsync("api/auth/validate-token", validateRequest);

            // Assert
            response.Should().NotBeNull();
            // Should not get 401 Unauthorized for endpoint access
            response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public async Task Scenario_UserRegistrationAndLogin_CompleteFlow()
        {
            // Arrange
            var registerDto = TestDataBuilder.CreateRegisterDto()
                .WithUsername("flowuser")
                .WithEmail("flowuser@example.com")
                .WithPassword("FlowPassword123")
                .WithConfirmPassword("FlowPassword123")
                .Build();

            // Act - Register
            var registerResponse = await _client.PostAsJsonAsync("api/auth/register", registerDto);

            // Assert
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // Act - Login
            var loginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("flowuser")
                .WithPassword("FlowPassword123")
                .Build();

            var loginResponse = await _client.PostAsJsonAsync("api/auth/login", loginDto);

            // Assert
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var tokenContent = await loginResponse.Content.ReadAsAsync<TokenResponseDto>();
            tokenContent!.AccessToken.Should().NotBeNullOrWhiteSpace();

            // Act - Get Profile
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenContent.AccessToken);
            var profileResponse = await _client.GetAsync("api/auth/profile");

            // Assert
            profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var profileContent = await profileResponse.Content.ReadAsAsync<UserResponseDto>();
            profileContent!.Username.Should().Be("flowuser");
        }

        [Fact]
        public async Task Scenario_MultipleUsersWithDifferentRoles()
        {
            // Test that different roles can be retrieved correctly
            var adminLogin = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var adminResponse = await _client.PostAsJsonAsync("api/auth/login", adminLogin);
            var adminToken = (await adminResponse.Content.ReadAsAsync<TokenResponseDto>())!;

            var cashierLogin = TestDataBuilder.CreateLoginDto()
                .WithUsername("cashier")
                .WithPassword("Cashier@123456")
                .Build();

            var cashierResponse = await _client.PostAsJsonAsync("api/auth/login", cashierLogin);
            var cashierToken = (await cashierResponse.Content.ReadAsAsync<TokenResponseDto>())!;

            // Assert different roles
            var adminClaims = JwtTestHelper.ExtractClaims(adminToken.AccessToken);
            var cashierClaims = JwtTestHelper.ExtractClaims(cashierToken.AccessToken);

            adminClaims.Should().ContainSingle(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "SuperAdmin");
            cashierClaims.Should().ContainSingle(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Cashier");
        }

        #endregion
    }
}
