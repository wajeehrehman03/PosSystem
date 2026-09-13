namespace PosWebApi.Models
{
    /// <summary>
    /// A return against a completed Order - full or partial, tracked per line item via RefundItems
    /// so an order can be partially refunded across multiple separate returns without ever
    /// exceeding the quantity originally sold (see RefundService.ProcessRefund).
    /// </summary>
    public class Refund
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        /// <summary>
        /// The shift this refund was processed under, for cash-drawer reconciliation - a refund
        /// requires an open shift for the same reason a sale does (see ShiftService.GenerateReport).
        /// </summary>
        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }

        public List<RefundItem> Items { get; set; } = new();

        /// <summary>How the refund was paid out. One of PaymentMethodConstants.</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Optional free-text reason (damaged, wrong item, changed mind, etc).</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Refunded subtotal/tax/total, each prorated from the original order's totals by the
        /// fraction of the order's subtotal being returned - keeps a partial refund consistent
        /// with however the original sale was actually priced (including any discount applied).
        /// </summary>
        public decimal SubtotalRefunded { get; set; }
        public decimal TaxRefunded { get; set; }
        public decimal TotalRefunded { get; set; }

        public string ProcessedByCashier { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A single returned line within a Refund, snapshotting the product as it was sold.</summary>
    public class RefundItem
    {
        public int Id { get; set; }

        public int RefundId { get; set; }
        public Refund? Refund { get; set; }

        /// <summary>The original OrderItem this return is against - used to enforce that the
        /// total refunded quantity for a line can never exceed what was originally sold.</summary>
        public int OrderItemId { get; set; }
        public OrderItem? OrderItem { get; set; }

        public int ProductId { get; set; }
        public string ProductNameSnapshot { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPriceSnapshot { get; set; }
        public decimal LineTotal { get; set; }
    }
}
