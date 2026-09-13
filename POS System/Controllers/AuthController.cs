using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Claims;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Authentication and user management endpoints.
    /// Provides user registration, login, and role-based access control.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AuditService _auditService;

        public AuthController(IAuthService authService, AuditService auditService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Registers a new user account.
        /// Only SuperAdmin users can register other SuperAdmin accounts.
        /// Cashier accounts can be registered by anyone (open registration).
        /// </summary>
        /// <param name="registerDto">Registration details including username, email, password, and optional role</param>
        /// <returns>Created user information with 201 Created status code</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (registerDto == null)
                    return BadRequest(new { error = "Request body is required" });

                if (!ModelState.IsValid)
                    return BadRequest(new { error = "Invalid input", details = ModelState });

                if (string.IsNullOrWhiteSpace(registerDto.Username))
                    return BadRequest(new { error = "Username is required" });

                if (string.IsNullOrWhiteSpace(registerDto.Email))
                    return BadRequest(new { error = "Email is required" });

                if (string.IsNullOrWhiteSpace(registerDto.Password))
                    return BadRequest(new { error = "Password is required" });

                if (registerDto.Password != registerDto.ConfirmPassword)
                    return BadRequest(new { error = "Password and confirm password do not match" });

                if (registerDto.Password.Length < 6)
                    return BadRequest(new { error = "Password must be at least 6 characters long" });

                // If a specific role is requested, verify the current user is SuperAdmin
                User? currentUser = null;
                if (!string.IsNullOrWhiteSpace(registerDto.Role))
                {
                    // Check if user is authenticated
                    if (User.Identity?.IsAuthenticated == true)
                    {
                        // Get current user ID from claims and retrieve the full user record so
                        // AuthService.RegisterAsync's own SuperAdmin check has a real user to
                        // evaluate (it always rejects a null currentUser).
                        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                        {
                            currentUser = await _authService.GetUserByIdAsync(userId);
                            if (currentUser == null || !currentUser.IsSuperAdmin())
                                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only SuperAdmin users can register other SuperAdmin accounts" });
                        }
                    }
                    else if (registerDto.Role.Equals(UserRoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                    {
                        // Non-authenticated users cannot create SuperAdmin accounts
                        return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only SuperAdmin users can register other SuperAdmin accounts" });
                    }
                }

                var result = await _authService.RegisterAsync(
                    registerDto.Username,
                    registerDto.Email,
                    registerDto.Password,
                    registerDto.Role,
                    currentUser
                );

                _auditService.Log("UserCreated", "User", result.Id, $"{result.Username} ({result.Role})");

                return CreatedAtAction(nameof(Login), new { username = result.Username }, new
                {
                    message = $"User '{result.Username}' registered successfully with role '{result.Role}'",
                    user = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Registration failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Authenticates a user and returns a JWT bearer token.
        /// The token can be used to access protected endpoints.
        /// </summary>
        /// <param name="loginDto">Login credentials (username and password)</param>
        /// <returns>JWT access token, token type, expiry time, and user information</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (loginDto == null)
                    return BadRequest(new { error = "Request body is required" });

                if (string.IsNullOrWhiteSpace(loginDto.Username))
                    return BadRequest(new { error = "Username is required" });

                if (string.IsNullOrWhiteSpace(loginDto.Password))
                    return BadRequest(new { error = "Password is required" });

                var result = await _authService.LoginAsync(loginDto.Username, loginDto.Password);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Login failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Logs out the current authenticated user.
        /// In a production system, this would invalidate the user's token.
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var usernameClaim = User.FindFirst(ClaimTypes.Name);
                var username = usernameClaim?.Value ?? "Unknown User";

                await _authService.LogoutAsync(username);

                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Logout failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets information about the currently authenticated user.
        /// Requires a valid JWT token in the Authorization header.
        /// </summary>
        /// <returns>Current user information including ID, username, email, and role</returns>
        [HttpGet("profile")]
        [Authorize]
        public IActionResult GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                var usernameClaim = User.FindFirst(ClaimTypes.Name);
                var emailClaim = User.FindFirst(ClaimTypes.Email);
                var roleClaim = User.FindFirst(ClaimTypes.Role);

                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                    return Unauthorized(new { error = "Invalid user claims in token" });

                return Ok(new
                {
                    id = userId,
                    username = usernameClaim?.Value,
                    email = emailClaim?.Value,
                    role = roleClaim?.Value,
                    isAuthenticated = User.Identity?.IsAuthenticated ?? false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve profile", details = ex.Message });
            }
        }

        /// <summary>
        /// Lists every user account. RESTRICTED: SuperAdmin only - used to pick a cashier
        /// when creating an account or inspecting their sales.
        /// </summary>
        /// <returns>All user accounts</returns>
        [HttpGet("users")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return Ok(new { items = users, count = users.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve users", details = ex.Message });
            }
        }

        /// <summary>
        /// Deactivates a Cashier account, blocking further logins immediately. RESTRICTED: SuperAdmin only.
        /// </summary>
        /// <param name="id">The user ID to deactivate</param>
        /// <returns>The updated user account</returns>
        [HttpPatch("users/{id}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            try
            {
                var user = await _authService.DeactivateUserAsync(id);
                _auditService.Log("UserDeactivated", "User", id, user.Username);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to deactivate user", details = ex.Message });
            }
        }

        /// <summary>
        /// Reactivates a previously deactivated Cashier account. RESTRICTED: SuperAdmin only.
        /// </summary>
        /// <param name="id">The user ID to activate</param>
        /// <returns>The updated user account</returns>
        [HttpPatch("users/{id}/activate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ActivateUser(int id)
        {
            try
            {
                var user = await _authService.ActivateUserAsync(id);
                _auditService.Log("UserActivated", "User", id, user.Username);
                return Ok(user);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to activate user", details = ex.Message });
            }
        }

        /// <summary>
        /// Permanently deletes a Cashier account. RESTRICTED: SuperAdmin only. Rejected with a
        /// conflict if the account has shift/order history - deactivate it instead.
        /// </summary>
        /// <param name="id">The user ID to delete</param>
        [HttpDelete("users/{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                await _authService.DeleteUserAsync(id);
                _auditService.Log("UserDeleted", "User", id, null);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.InnerException is DbUpdateException dbEx
                && dbEx.InnerException is MySqlException mysqlEx
                && mysqlEx.ErrorCode == MySqlErrorCode.RowIsReferenced2)
            {
                return Conflict(new { error = "Cannot delete a user with existing shift or order history. Deactivate the account instead." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete user", details = ex.Message });
            }
        }

        /// <summary>
        /// Validates a JWT token without using it for authentication.
        /// Useful for checking token validity before making requests.
        /// </summary>
        /// <param name="request">Object containing the token to validate</param>
        /// <returns>Token validity status</returns>
        [HttpPost("validate-token")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Token))
                    return BadRequest(new { error = "Token is required" });

                var isValid = await _authService.ValidateTokenAsync(request.Token);

                return Ok(new { isValid, message = isValid ? "Token is valid" : "Token is invalid or expired" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Token validation failed", details = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request model for token validation endpoint.
    /// </summary>
    public class TokenValidationRequest
    {
        /// <summary>
        /// The JWT token to validate.
        /// </summary>
        public string? Token { get; set; }
    }
}
