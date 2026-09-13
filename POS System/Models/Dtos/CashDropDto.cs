namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for recording a cash drop to the safe during an open shift.
    /// </summary>
    public class CashDropDto
    {
        /// <summary>
        /// Amount of cash removed from the drawer. Must be greater than 0.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional free-text note (e.g. "midday drop", reason for the movement).
        /// </summary>
        public string? Note { get; set; }
    }
}
