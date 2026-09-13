namespace PosWebApi.Models
{
    /// <summary>
    /// Enumeration of application roles for Role-Based Access Control (RBAC).
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Cashier role - Can perform sales transactions, view catalog, manage cart and checkout.
        /// </summary>
        Cashier = 0,

        /// <summary>
        /// SuperAdmin role - Full administrative access including user management, catalog management, and reporting.
        /// </summary>
        SuperAdmin = 1
    }

    /// <summary>
    /// Helper class for role-related constants and validation.
    /// </summary>
    public static class UserRoleConstants
    {
        public const string Cashier = "Cashier";
        public const string SuperAdmin = "SuperAdmin";

        /// <summary>
        /// Array of all valid role names for validation and authorization.
        /// </summary>
        public static readonly string[] AllRoles = { Cashier, SuperAdmin };

        /// <summary>
        /// Validates if a role string is valid.
        /// </summary>
        /// <param name="role">The role string to validate</param>
        /// <returns>True if the role is valid; otherwise, false</returns>
        public static bool IsValidRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return false;

            return AllRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts a role string to the normalized form (matching enum).
        /// </summary>
        /// <param name="role">The role string to normalize</param>
        /// <returns>The normalized role string, or null if invalid</returns>
        public static string? NormalizeRole(string role)
        {
            if (!IsValidRole(role))
                return null;

            // Return the properly-cased version
            if (role.Equals(Cashier, StringComparison.OrdinalIgnoreCase))
                return Cashier;

            if (role.Equals(SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return SuperAdmin;

            return null;
        }

        /// <summary>
        /// Converts UserRole enum to string representation.
        /// </summary>
        /// <param name="role">The UserRole enum value</param>
        /// <returns>The string representation of the role</returns>
        public static string ToString(UserRole role)
        {
            return role switch
            {
                UserRole.Cashier => Cashier,
                UserRole.SuperAdmin => SuperAdmin,
                _ => Cashier
            };
        }

        /// <summary>
        /// Converts string to UserRole enum.
        /// </summary>
        /// <param name="role">The role string to convert</param>
        /// <param name="userRole">The resulting UserRole enum value</param>
        /// <returns>True if conversion succeeded; otherwise, false</returns>
        public static bool TryParse(string role, out UserRole userRole)
        {
            userRole = UserRole.Cashier;

            if (string.IsNullOrWhiteSpace(role))
                return false;

            if (role.Equals(SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                userRole = UserRole.SuperAdmin;
                return true;
            }

            if (role.Equals(Cashier, StringComparison.OrdinalIgnoreCase))
            {
                userRole = UserRole.Cashier;
                return true;
            }

            return false;
        }
    }
}
