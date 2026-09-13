namespace PosWebApi.Models
{
    public class User
    {
        public int Id { get; set; }

        /// <summary>
        /// Username for authentication. Must be unique and between 3-100 characters.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Email address for user identification and communication.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// BCrypt-hashed password. Never stored in plaintext.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// User's role for Role-Based Access Control (RBAC).
        /// Defaults to "Cashier". Valid values: "Cashier", "SuperAdmin"
        /// </summary>
        public string Role { get; set; } = UserRoleConstants.Cashier;

        /// <summary>
        /// Account creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indicates whether the user account is active. Inactive accounts cannot authenticate.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Optional: Timestamp of last successful login (UTC).
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Validates that the Role property contains a valid role value.
        /// </summary>
        /// <returns>True if the role is valid; otherwise, false</returns>
        public bool IsRoleValid()
        {
            return UserRoleConstants.IsValidRole(Role);
        }

        /// <summary>
        /// Normalizes the Role property to the standard format.
        /// </summary>
        public void NormalizeRole()
        {
            var normalized = UserRoleConstants.NormalizeRole(Role);
            if (normalized != null)
            {
                Role = normalized;
            }
        }

        /// <summary>
        /// Checks if the user has a specific role.
        /// </summary>
        /// <param name="role">The role to check (case-insensitive)</param>
        /// <returns>True if the user has the specified role; otherwise, false</returns>
        public bool HasRole(string role)
        {
            return Role.Equals(role, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the user is a SuperAdmin.
        /// </summary>
        /// <returns>True if the user is a SuperAdmin; otherwise, false</returns>
        public bool IsSuperAdmin()
        {
            return HasRole(UserRoleConstants.SuperAdmin);
        }

        /// <summary>
        /// Checks if the user is a Cashier.
        /// </summary>
        /// <returns>True if the user is a Cashier; otherwise, false</returns>
        public bool IsCashier()
        {
            return HasRole(UserRoleConstants.Cashier);
        }
    }
}
