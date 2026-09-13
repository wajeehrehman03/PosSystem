namespace PosWebApi.Models.Dtos
{
    /// <summary>One requested return line within a refund request.</summary>
    public class RefundItemRequestDto
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>Request body for POST api/orders/{id}/refund.</summary>
    public class RefundRequestDto
    {
        public List<RefundItemRequestDto> Items { get; set; } = new();

        /// <summary>How the refund is paid out. See PaymentMethodConstants.</summary>
        public required string Method { get; set; }

        public string? Reason { get; set; }
        public string? CashierName { get; set; }
    }
}
