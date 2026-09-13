using System;
using System.Collections.Generic;

namespace PosWebApi.Models
{
    public class Order
    {
        public int Id { get; set; }
        public Guid OrderNumber { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Line items for this order, each snapshotting the product's name/price as they were
        /// at checkout time (see OrderItem) rather than referencing the live, possibly-since-
        /// changed Product.
        /// </summary>
        public List<OrderItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Total change returned to the customer, drawn from a cash tender that exceeded the
        /// order total. Always zero when payment was exact or entirely card/gift card, since
        /// those can never overpay (enforced in PosEngine.Checkout).
        /// </summary>
        public decimal ChangeDue { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string ProcessedByCashier { get; set; } = string.Empty;

        /// <summary>
        /// The shift this order's sale was rung up under, for cash reconciliation
        /// (X/Z-Reports). Nullable because older orders predate shift tracking.
        /// </summary>
        public int? ShiftId { get; set; }
        public Shift? Shift { get; set; }

        /// <summary>
        /// The tenders (cash/card/gift card) applied to this order. Multiple rows support
        /// split-tender sales.
        /// </summary>
        public List<Payment> Payments { get; set; } = new();

        /// <summary>
        /// The loyalty customer this sale was attributed to, if any (anonymous checkout leaves
        /// this null - loyalty/discount fields are entirely optional).
        /// </summary>
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        /// <summary>The promotional code applied to this order, if any.</summary>
        public int? DiscountCodeId { get; set; }
        public DiscountCode? DiscountCode { get; set; }

        /// <summary>
        /// Total discount applied (from a discount code and/or points redemption, summed and
        /// clamped so it can never exceed Subtotal). Tax is calculated on Subtotal minus this,
        /// not on the raw Subtotal.
        /// </summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>Loyalty points earned by this sale (0 if no Customer, or Customer opted not to earn).</summary>
        public int PointsEarned { get; set; }

        /// <summary>Loyalty points redeemed toward this sale's DiscountAmount.</summary>
        public int PointsRedeemed { get; set; }

        /// <summary>
        /// Idempotency key for a sale replayed via PosEngine.ProcessQueuedSale (offline-first
        /// sync) - the register generates this once per queued sale and retries with the same
        /// value until sync succeeds. Null for a normal online checkout, which has no need for
        /// one. Unique where not null so a retried sync can never create a duplicate order.
        /// </summary>
        public string? ClientTransactionId { get; set; }
    }
}