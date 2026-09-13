namespace PosWebApi.Models.Dtos
{
    public class RefundResponseDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ShiftId { get; set; }
        public required string Method { get; set; }
        public string? Reason { get; set; }
        public decimal SubtotalRefunded { get; set; }
        public decimal TaxRefunded { get; set; }
        public decimal TotalRefunded { get; set; }
        public required string ProcessedByCashier { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<RefundItemResponseDto> Items { get; set; } = new();

        public static RefundResponseDto FromRefund(Refund refund)
        {
            if (refund == null)
                throw new ArgumentNullException(nameof(refund));

            return new RefundResponseDto
            {
                Id = refund.Id,
                OrderId = refund.OrderId,
                ShiftId = refund.ShiftId,
                Method = refund.Method,
                Reason = refund.Reason,
                SubtotalRefunded = refund.SubtotalRefunded,
                TaxRefunded = refund.TaxRefunded,
                TotalRefunded = refund.TotalRefunded,
                ProcessedByCashier = refund.ProcessedByCashier,
                CreatedAt = refund.CreatedAt,
                Items = refund.Items.Select(i => new RefundItemResponseDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductNameSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPriceSnapshot,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }
    }

    public class RefundItemResponseDto
    {
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
