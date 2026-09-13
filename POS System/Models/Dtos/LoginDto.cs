namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for user login requests.
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// Username for authentication.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Password for authentication.
        /// </summary>
        public required string Password { get; set; }
    }
}

