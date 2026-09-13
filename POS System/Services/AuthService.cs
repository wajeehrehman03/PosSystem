using PosWebApi.Data;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace PosWebApi.Services
{
    /// <summary>
    /// Authentication service handling user registration, login, and JWT token generation.
    /// Implements role-based access control with two roles: SuperAdmin and Cashier.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IGenericRepository<User> _userRepository;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(IGenericRepository<User> userRepository, AppDbContext context, IConfiguration configuration)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Registers a new user account. Only SuperAdmin users can register other SuperAdmin accounts.
        /// </summary>
        /// <param name="username">Username for the new account (must be unique)</param>
        /// <param name="email">Email address for the new account</param>
        /// <param name="password">Password for the new account</param>
        /// <param name="role">Optional role for the new account (default: Cashier)</param>
        /// <param name="currentUser">Current authenticated user (if any) for permission checks</param>
        /// <returns>The created user information as UserResponseDto</returns>
        public async Task<UserResponseDto> RegisterAsync(string username, string email, string password, string? role = null, User? currentUser = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Username and password are required");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required");

            if (username.Length < 3 || username.Length > 100)
                throw new ArgumentException("Username must be between 3 and 100 characters");

            if (password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters long");

            // Check for duplicate username (case-insensitive)
            var existingUser = _userRepository.GetAll().FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (existingUser != null)
                throw new InvalidOperationException($"Username '{username}' is already registered");

            // Determine the role for the new account
            string userRole = UserRoleConstants.Cashier; // Default role

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = UserRoleConstants.NormalizeRole(role);
                if (normalizedRole == null)
                    throw new ArgumentException($"Invalid role '{role}'. Valid roles are: {string.Join(", ", UserRoleConstants.AllRoles)}");

                // Only SuperAdmin can create SuperAdmin accounts
                if (normalizedRole == UserRoleConstants.SuperAdmin)
                {
                    if (currentUser == null || !currentUser.IsSuperAdmin())
                        throw new UnauthorizedAccessException("Only SuperAdmin users can create other SuperAdmin accounts");
                }

                userRole = normalizedRole;
            }

            var user = new User
            {
                Username = username.Trim(),
                Email = email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = userRole,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Add user to repository and explicitly save changes
            _userRepository.Add(user);
            await _context.SaveChangesAsync();

            // Refresh the user object to ensure we have the generated ID
            var createdUser = _userRepository.GetAll().FirstOrDefault(u => u.Username == user.Username);
            if (createdUser == null)
                throw new InvalidOperationException("Failed to retrieve created user from database");

            return UserResponseDto.FromUser(createdUser);
        }

        /// <summary>
        /// Authenticates a user by username and password, and returns a JWT token.
        /// </summary>
        /// <param name="username">Username to authenticate</param>
        /// <param name="password">Password to authenticate</param>
        /// <returns>TokenResponseDto containing JWT access token and user information</returns>
        public async Task<TokenResponseDto> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Username and password are required");

            var user = _userRepository.GetAll().FirstOrDefault(u => u.Username == username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid username or password");

            if (!user.IsActive)
                throw new UnauthorizedAccessException("User account is inactive");

            // Generate JWT token
            var token = GenerateJwtToken(user);

            // Update last login timestamp
            user.LastLoginAt = DateTime.UtcNow;
            _userRepository.Update(user);

            var userResponse = UserResponseDto.FromUser(user);

            return new TokenResponseDto
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = GetTokenExpirySeconds(),
                User = userResponse,
                Message = "Authentication successful"
            };
        }

        /// <summary>
        /// Retrieves a user by ID.
        /// </summary>
        /// <param name="userId">The user ID to retrieve</param>
        /// <returns>The User entity if found; otherwise, null</returns>
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            if (userId <= 0)
                return await Task.FromResult<User?>(null);

            return await Task.FromResult(_userRepository.GetById(userId));
        }

        /// <summary>
        /// Retrieves every user account, ordered by username.
        /// </summary>
        /// <returns>All users as UserResponseDto</returns>
        public async Task<List<UserResponseDto>> GetAllUsersAsync()
        {
            var users = _userRepository.GetAll()
                .OrderBy(u => u.Username)
                .Select(UserResponseDto.FromUser)
                .ToList();

            return await Task.FromResult(users);
        }

        /// <summary>
        /// Deactivates a Cashier account. Restricted to Cashier accounts - SuperAdmin accounts
        /// aren't managed through this path.
        /// </summary>
        /// <param name="userId">The user ID to deactivate</param>
        /// <returns>The updated user as UserResponseDto</returns>
        public async Task<UserResponseDto> DeactivateUserAsync(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            if (!user.IsCashier())
                throw new UnauthorizedAccessException("Only Cashier accounts can be deactivated through this endpoint");

            if (!user.IsActive)
                throw new InvalidOperationException($"User '{user.Username}' is already inactive");

            user.IsActive = false;
            _userRepository.Update(user);

            return await Task.FromResult(UserResponseDto.FromUser(user));
        }

        /// <summary>
        /// Reactivates a previously deactivated Cashier account. Restricted to Cashier accounts.
        /// </summary>
        /// <param name="userId">The user ID to activate</param>
        /// <returns>The updated user as UserResponseDto</returns>
        public async Task<UserResponseDto> ActivateUserAsync(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            if (!user.IsCashier())
                throw new UnauthorizedAccessException("Only Cashier accounts can be activated through this endpoint");

            if (user.IsActive)
                throw new InvalidOperationException($"User '{user.Username}' is already active");

            user.IsActive = true;
            _userRepository.Update(user);

            return await Task.FromResult(UserResponseDto.FromUser(user));
        }

        /// <summary>
        /// Permanently deletes a Cashier account. Restricted to Cashier accounts.
        /// </summary>
        /// <param name="userId">The user ID to delete</param>
        public async Task DeleteUserAsync(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            if (!user.IsCashier())
                throw new UnauthorizedAccessException("Only Cashier accounts can be deleted through this endpoint");

            // Wraps a MySQL FK-restrict violation (Shift.CashierId is Restrict) as
            // InvalidOperationException, same as GenericRepository.Delete/CatalogManager.Delete
            // does for products with order history.
            _userRepository.Delete(userId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates a JWT token without attempting to use it for authentication.
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>True if the token is valid; otherwise, false</returns>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return await Task.FromResult(false);

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = GetSymmetricSecurityKey();

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return await Task.FromResult(true);
            }
            catch
            {
                return await Task.FromResult(false);
            }
        }

        /// <summary>
        /// Logs out a user (currently a placeholder for token revocation logic).
        /// </summary>
        /// <param name="username">Username of the user to log out</param>
        /// <returns>Completed task</returns>
        public async Task LogoutAsync(string username)
        {
            // In a real system, you might:
            // 1. Add the token to a blacklist cache
            // 2. Invalidate refresh tokens
            // 3. Clear session data
            await Task.CompletedTask;
        }

        /// <summary>
        /// Generates a JWT token for the given user with role claims.
        /// </summary>
        /// <param name="user">The user for which to generate the token</param>
        /// <returns>The JWT token as a string</returns>
        private string GenerateJwtToken(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var key = GetSymmetricSecurityKey();
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Create claims for the token
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "15")),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Gets the symmetric security key used for JWT signing/validation.
        /// The key is read from appsettings.json (Jwt:Secret) and must be at least 256 bits (32 bytes).
        /// </summary>
        /// <returns>A SymmetricSecurityKey for JWT operations</returns>
        private SymmetricSecurityKey GetSymmetricSecurityKey()
        {
            var secret = _configuration["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("JWT secret is not configured in appsettings.json");

            var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("JWT secret must be at least 256 bits (32 bytes)");

            return new SymmetricSecurityKey(keyBytes);
        }

        /// <summary>
        /// Gets the token expiry duration in seconds.
        /// </summary>
        /// <returns>The token expiry in seconds</returns>
        private int GetTokenExpirySeconds()
        {
            var minutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "15");
            return minutes * 60;
        }
    }
}
