using PosWebApi.Models;
using PosWebApi.Models.Dtos;

namespace PosWebApi.Services
{
    /// <summary>
    /// Interface for authentication and authorization operations.
    /// Handles user registration, login, token generation, and validation.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user account.
        /// </summary>
        /// <param name="username">Username for the new account</param>
        /// <param name="email">Email address for the new account</param>
        /// <param name="password">Password for the new account</param>
        /// <param name="role">Optional role for the new account (default: Cashier)</param>
        /// <param name="currentUser">Current authenticated user (if any) for permission checks</param>
        /// <returns>UserResponseDto containing the created user information</returns>
        Task<UserResponseDto> RegisterAsync(string username, string email, string password, string? role = null, User? currentUser = null);

        /// <summary>
        /// Authenticates a user and returns a JWT token.
        /// </summary>
        /// <param name="username">Username to authenticate</param>
        /// <param name="password">Password to authenticate</param>
        /// <returns>TokenResponseDto containing JWT token and user information</returns>
        Task<TokenResponseDto> LoginAsync(string username, string password);

        /// <summary>
        /// Retrieves a user by ID. Used for authorization checks (e.g. verifying the calling
        /// user's role from the database before allowing a privileged action) rather than
        /// trusting claims alone.
        /// </summary>
        /// <param name="userId">The user ID to retrieve</param>
        /// <returns>The User entity if found; otherwise, null</returns>
        Task<User?> GetUserByIdAsync(int userId);

        /// <summary>
        /// Retrieves every user account, for SuperAdmin user-management views (e.g. picking a
        /// cashier to inspect sales for).
        /// </summary>
        /// <returns>All users as UserResponseDto, ordered by username</returns>
        Task<List<UserResponseDto>> GetAllUsersAsync();

        /// <summary>
        /// Deactivates a Cashier account, blocking further logins immediately (LoginAsync checks
        /// IsActive). Restricted to Cashier accounts - SuperAdmin accounts aren't managed here.
        /// </summary>
        /// <param name="userId">The user ID to deactivate</param>
        /// <returns>The updated user as UserResponseDto</returns>
        Task<UserResponseDto> DeactivateUserAsync(int userId);

        /// <summary>
        /// Reactivates a previously deactivated Cashier account. Restricted to Cashier accounts.
        /// </summary>
        /// <param name="userId">The user ID to activate</param>
        /// <returns>The updated user as UserResponseDto</returns>
        Task<UserResponseDto> ActivateUserAsync(int userId);

        /// <summary>
        /// Permanently deletes a Cashier account. Restricted to Cashier accounts. Fails with
        /// InvalidOperationException if the account has shift/order history the database's
        /// FK-restrict constraints protect (deactivate instead in that case).
        /// </summary>
        /// <param name="userId">The user ID to delete</param>
        Task DeleteUserAsync(int userId);

        /// <summary>
        /// Validates a JWT token.
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>True if the token is valid; otherwise, false</returns>
        Task<bool> ValidateTokenAsync(string token);

        /// <summary>
        /// Logs out a user (placeholder for token revocation logic).
        /// </summary>
        /// <param name="username">Username of the user to log out</param>
        /// <returns>Completed task</returns>
        Task LogoutAsync(string username);
    }
}
