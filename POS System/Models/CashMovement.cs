namespace PosWebApi.Models
{
    /// <summary>
    /// Records a manual cash movement against an open shift's drawer (a drop to the safe,
    /// or a pay-in/pay-out) that isn't a sale. Feeds into the shift's X/Z-Report math.
    /// </summary>
    public class CashMovement
    {
        public int Id { get; set; }

        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }

        /// <summary>
        /// One of CashMovementTypeConstants. Direction (add/subtract from expected cash)
        /// is implied by the type, not stored separately.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Always positive; the Type determines whether this adds to or subtracts from
        /// the drawer's expected cash total.
        /// </summary>
        public decimal Amount { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Helper class for cash movement type constants and validation, mirroring UserRoleConstants.
    /// </summary>
    public static class CashMovementTypeConstants
    {
        /// <summary>Cash removed from the drawer and taken to the safe.</summary>
        public const string CashDrop = "CashDrop";

        /// <summary>Cash added to the drawer from outside a sale (e.g. change fund top-up).</summary>
        public const string PayIn = "PayIn";

        /// <summary>Cash removed from the drawer for a non-sale expense.</summary>
        public const string PayOut = "PayOut";

        public static readonly string[] AllTypes = { CashDrop, PayIn, PayOut };

        public static bool IsValidType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            return AllTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
        }
    }
}
