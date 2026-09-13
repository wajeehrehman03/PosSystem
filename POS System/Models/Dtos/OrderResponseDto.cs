namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for a completed order's full details (line items, payments, shift).
    /// </summary>
    public class OrderResponseDto
    {
        public int Id { get; set; }
        public Guid OrderNumber { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ChangeDue { get; set; }
        public DateTime OrderDate { get; set; }
        public required string ProcessedByCashier { get; set; }
        public int? ShiftId { get; set; }
        public int? CustomerId { get; set; }
        public int? DiscountCodeId { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsEarned { get; set; }
        public int PointsRedeemed { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new();
        public List<PaymentResponseDto> Payments { get; set; } = new();

        /// <param name="refundedByOrderItemId">Optional map of OrderItem.Id to quantity already
        /// refunded across all prior refunds, so a caller can show how much of each line remains
        /// returnable. Omitted (or an OrderItem missing from it) means "0 refunded".</param>
        public static OrderResponseDto FromOrder(Order order, IReadOnlyDictionary<int, int>? refundedByOrderItemId = null)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return new OrderResponseDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Subtotal = order.Subtotal,
                TaxAmount = order.TaxAmount,
                TotalAmount = order.TotalAmount,
                ChangeDue = order.ChangeDue,
                OrderDate = order.OrderDate,
                ProcessedByCashier = order.ProcessedByCashier,
                ShiftId = order.ShiftId,
                CustomerId = order.CustomerId,
                DiscountCodeId = order.DiscountCodeId,
                DiscountAmount = order.DiscountAmount,
                PointsEarned = order.PointsEarned,
                PointsRedeemed = order.PointsRedeemed,
                Items = order.Items.Select(i => new OrderItemResponseDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductNameSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPriceSnapshot,
                    LineTotal = i.LineTotal,
                    QuantityRefunded = refundedByOrderItemId != null && refundedByOrderItemId.TryGetValue(i.Id, out var refundedQty)
                        ? refundedQty
                        : 0
                }).ToList(),
                Payments = order.Payments.Select(p => new PaymentResponseDto
                {
                    Method = p.Method,
                    Amount = p.Amount,
                    ReferenceNumber = p.ReferenceNumber
                }).ToList()
            };
        }
    }

    public class OrderItemResponseDto
    {
        /// <summary>The OrderItem's own ID - pass this back as RefundItemRequestDto.OrderItemId
        /// when processing a return against this line.</summary>
        public int Id { get; set; }
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        /// <summary>Quantity of this line already refunded across all prior refunds.</summary>
        public int QuantityRefunded { get; set; }
    }

    public class PaymentResponseDto
    {
        public required string Method { get; set; }
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
    }
}
