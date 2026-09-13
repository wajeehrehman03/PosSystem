namespace PosWebApi.Models
{
    /// <summary>
    /// A product+quantity line item for a sale being processed - either sourced from the
    /// caller's persisted cart (PosEngine.Checkout) or submitted directly by an offline-queued
    /// sale (PosEngine.ProcessQueuedSale, which has no server-side cart to draw from since the
    /// register was disconnected when the sale happened).
    /// </summary>
    public class SaleLineItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
