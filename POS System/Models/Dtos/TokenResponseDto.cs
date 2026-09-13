namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for JWT authentication response containing token and user information.
    /// </summary>
    public class TokenResponseDto
    {
        /// <summary>
        /// JWT access token for API authentication.
        /// Include this token in the Authorization header as: Authorization: Bearer {token}
        /// </summary>
        public required string AccessToken { get; set; }

        /// <summary>
        /// Type of token (typically "Bearer").
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// Expiration time of the token in seconds from now.
        /// </summary>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Information about the authenticated user.
        /// </summary>
        public required UserResponseDto User { get; set; }

        /// <summary>
        /// Success message.
        /// </summary>
        public string Message { get; set; } = "Authentication successful";
    }
}
