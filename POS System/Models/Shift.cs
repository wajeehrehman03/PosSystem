namespace PosWebApi.Models
{
    /// <summary>
    /// Represents a cashier's working session on a register, from opening the cash float
    /// to closing out with a counted-cash reconciliation (Z-Report).
    /// </summary>
    public class Shift
    {
        public int Id { get; set; }

        /// <summary>
        /// The cashier (or SuperAdmin) working this shift.
        /// </summary>
        public int CashierId { get; set; }
        public User? Cashier { get; set; }

        /// <summary>The physical register/terminal this shift was opened on.</summary>
        public int RegisterId { get; set; }
        public Register? Register { get; set; }

        /// <summary>
        /// Starting cash placed in the drawer when the shift opens.
        /// </summary>
        public decimal OpeningFloat { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }

        /// <summary>
        /// Cash physically counted in the drawer at close time (entered by the cashier/admin).
        /// Null until the shift is closed.
        /// </summary>
        public decimal? ClosingCountedAmount { get; set; }

        public string Status { get; set; } = ShiftStatusConstants.Open;

        public bool IsOpen()
        {
            return Status.Equals(ShiftStatusConstants.Open, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Helper class for shift status constants and validation, mirroring UserRoleConstants.
    /// </summary>
    public static class ShiftStatusConstants
    {
        public const string Open = "Open";
        public const string Closed = "Closed";
    }
}
