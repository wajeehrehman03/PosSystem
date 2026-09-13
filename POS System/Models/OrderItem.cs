namespace PosWebApi.Models
{
    /// <summary>
    /// A single line item on a completed Order. Snapshots the product's name and price as they
    /// were at the moment of sale (rather than referencing the live Product), so that a receipt
    /// or order-history lookup is accurate even if the product is later repriced, renamed, or
    /// deleted from the catalog.
    /// </summary>
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int ProductId { get; set; }

        /// <summary>Product name at the time of sale.</summary>
        public string ProductNameSnapshot { get; set; } = string.Empty;

        public int Quantity { get; set; }

        /// <summary>Unit price charged at the time of sale (not the product's current price).</summary>
        public decimal UnitPriceSnapshot { get; set; }

        /// <summary>UnitPriceSnapshot * Quantity, stored rather than computed so historical
        /// receipts remain correct even if the calculation logic changes later.</summary>
        public decimal LineTotal { get; set; }
    }
}
