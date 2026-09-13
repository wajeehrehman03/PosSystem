namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for user registration requests.
    /// </summary>
    public class RegisterDto
    {
        /// <summary>
        /// Username for the new account. Must be unique and 3-100 characters.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Email address for the new account.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Password for the new account. Minimum 6 characters recommended.
        /// </summary>
        public required string Password { get; set; }

        /// <summary>
        /// Password confirmation to prevent typos.
        /// </summary>
        public required string ConfirmPassword { get; set; }

        /// <summary>
        /// Optional: Role for the new account. Only SuperAdmin users can set this.
        /// Defaults to "Cashier" if not provided or invalid.
        /// Valid values: "Cashier", "SuperAdmin"
        /// </summary>
        public string? Role { get; set; }
    }
}

