namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Request body for POST api/pos/checkout. Supports split-tender sales via multiple
    /// entries in Tenders (e.g. part cash, part card).
    /// </summary>
    public class CheckoutRequestDto
    {
        public string? CashierName { get; set; }
        public List<PaymentRequestDto> Tenders { get; set; } = new();

        /// <summary>Optional loyalty customer to attribute this sale to (looked up by phone).</summary>
        public string? CustomerPhone { get; set; }

        /// <summary>Optional promotional code to apply.</summary>
        public string? DiscountCode { get; set; }

        /// <summary>Optional loyalty points to redeem toward this sale. Requires CustomerPhone.</summary>
        public int? PointsToRedeem { get; set; }
    }
}
