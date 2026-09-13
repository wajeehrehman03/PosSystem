namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for closing a shift with the cashier's physically counted cash.
    /// </summary>
    public class CloseShiftDto
    {
        /// <summary>
        /// Cash physically counted in the drawer at close time.
        /// </summary>
        public decimal ClosingCountedAmount { get; set; }
    }
}
