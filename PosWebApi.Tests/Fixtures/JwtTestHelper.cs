using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using PosWebApi.Models;

namespace PosWebApi.Tests.Fixtures
{
    /// <summary>
    /// Helper class for generating and validating JWT tokens in tests.
    /// </summary>
    public static class JwtTestHelper
    {
        private static readonly string TestSecret = "TestOnlyNotForProductionSecretKey32BytesLong";
        private static readonly string TestIssuer = "PosSystemApi";
        private static readonly string TestAudience = "PosSystemClient";
        private const int TestExpiryMinutes = 15;

        /// <summary>
        /// Generates a valid JWT token for testing.
        /// </summary>
        public static string GenerateTestToken(User user, int expiryMinutes = TestExpiryMinutes)
        {
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: TestAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Generates an expired JWT token (useful for testing token expiry scenarios).
        /// </summary>
        public static string GenerateExpiredToken(User user)
        {
            return GenerateTestToken(user, expiryMinutes: -15);
        }

        /// <summary>
        /// Generates a token with invalid signature (tampered token).
        /// </summary>
        public static string GenerateTamperedToken(User user)
        {
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("WrongSecretKeyNotReallyValidForHS256Tokens"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: TestAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Gets the test configuration for JWT validation.
        /// </summary>
        public static (string Secret, string Issuer, string Audience, int ExpiryMinutes) GetTestConfiguration()
        {
            return (TestSecret, TestIssuer, TestAudience, TestExpiryMinutes);
        }

        /// <summary>
        /// Extracts claims from a JWT token string (useful for assertion in tests).
        /// </summary>
        public static List<Claim> ExtractClaims(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            return jsonToken?.Claims?.ToList() ?? new List<Claim>();
        }
    }
}
