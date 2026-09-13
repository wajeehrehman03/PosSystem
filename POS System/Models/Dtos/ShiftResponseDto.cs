namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for shift information in responses.
    /// </summary>
    public class ShiftResponseDto
    {
        public int Id { get; set; }
        public int CashierId { get; set; }
        public required string RegisterCode { get; set; }
        public decimal OpeningFloat { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal? ClosingCountedAmount { get; set; }
        public required string Status { get; set; }

        /// <summary>
        /// Creates a ShiftResponseDto from a Shift entity.
        /// </summary>
        public static ShiftResponseDto FromShift(Shift shift)
        {
            if (shift == null)
                throw new ArgumentNullException(nameof(shift));

            return new ShiftResponseDto
            {
                Id = shift.Id,
                CashierId = shift.CashierId,
                RegisterCode = shift.Register?.Code ?? string.Empty,
                OpeningFloat = shift.OpeningFloat,
                OpenedAt = shift.OpenedAt,
                ClosedAt = shift.ClosedAt,
                ClosingCountedAmount = shift.ClosingCountedAmount,
                Status = shift.Status
            };
        }
    }
}
