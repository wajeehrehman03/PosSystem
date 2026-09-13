namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for discount code information in responses.
    /// </summary>
    public class DiscountCodeResponseDto
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Type { get; set; }
        public decimal Value { get; set; }
        public decimal? MinSubtotal { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }

        public static DiscountCodeResponseDto FromDiscountCode(DiscountCode discountCode)
        {
            if (discountCode == null)
                throw new ArgumentNullException(nameof(discountCode));

            return new DiscountCodeResponseDto
            {
                Id = discountCode.Id,
                Code = discountCode.Code,
                Type = discountCode.Type,
                Value = discountCode.Value,
                MinSubtotal = discountCode.MinSubtotal,
                ExpiresAt = discountCode.ExpiresAt,
                IsActive = discountCode.IsActive
            };
        }
    }
}
