namespace PosWebApi.Models.Dtos
{
    /// <summary>A single ProductId+Quantity line item within a QueuedSaleDto.</summary>
    public class QueuedSaleItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// A single offline-queued sale submitted for replay via POST api/sync/sales. Sourced from a
    /// register's local queue rather than a server-side cart (the register was disconnected when
    /// the sale happened, so it could never call add-to-cart against the server).
    /// </summary>
    public class QueuedSaleDto
    {
        /// <summary>
        /// The register's locally-generated idempotency key for this sale (e.g. a GUID string).
        /// Retrying sync with the same value is always safe - see SyncService.
        /// </summary>
        public string ClientTransactionId { get; set; } = string.Empty;

        /// <summary>When the sale actually happened offline (UTC), not when it's synced.</summary>
        public DateTime OccurredAt { get; set; }

        public string? CashierName { get; set; }

        public List<QueuedSaleItemDto> Items { get; set; } = new();

        public List<PaymentRequestDto> Tenders { get; set; } = new();

        public string? CustomerPhone { get; set; }

        public string? DiscountCode { get; set; }

        public int? PointsToRedeem { get; set; }
    }
}
