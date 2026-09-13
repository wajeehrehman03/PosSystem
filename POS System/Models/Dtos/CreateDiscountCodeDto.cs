namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Request body for POST api/discount-codes. See DiscountCodeTypeConstants for valid Type values.
    /// </summary>
    public class CreateDiscountCodeDto
    {
        public required string Code { get; set; }
        public required string Type { get; set; }
        public decimal Value { get; set; }
        public decimal? MinSubtotal { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
