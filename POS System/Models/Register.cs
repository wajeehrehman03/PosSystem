namespace PosWebApi.Models
{
    /// <summary>
    /// A physical register/terminal a shift can be opened against. Replaces the old free-text
    /// Shift.RegisterId string with a real, manageable entity - lets a shift be validated against
    /// a real, active register, and enables reporting sales by register/location.
    /// </summary>
    public class Register
    {
        public int Id { get; set; }

        /// <summary>Short human-entered identifier, e.g. "REG-1". Unique.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Optional display name, e.g. "Front Counter".</summary>
        public string? Name { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
