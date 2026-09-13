namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for user information in responses (without sensitive data).
    /// </summary>
    public class UserResponseDto
    {
        /// <summary>
        /// Unique identifier for the user.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Username of the user.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Email address of the user.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Role of the user (Cashier or SuperAdmin).
        /// </summary>
        public required string Role { get; set; }

        /// <summary>
        /// Account creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Indicates whether the user account is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Last successful login timestamp (UTC), or null if never logged in.
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Creates a UserResponseDto from a User entity.
        /// </summary>
        /// <param name="user">The User entity to convert</param>
        /// <returns>A new UserResponseDto with data from the user entity</returns>
        public static UserResponseDto FromUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}
