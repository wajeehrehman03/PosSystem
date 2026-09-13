using System.ComponentModel.DataAnnotations.Schema;

namespace PosWebApi.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        /// <summary>
        /// The user (cashier/admin) this cart item belongs to. Cart contents are persisted
        /// per-user so they survive across requests (each request gets a fresh DI scope).
        /// </summary>
        public int UserId { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int Quantity { get; set; }

        /// <summary>
        /// False while the item is part of an active, unpaid cart. Historical order lines are
        /// now captured as OrderItem snapshots at checkout time (see PosEngine.Checkout), and
        /// the CartItem row is deleted rather than retained - this flag exists only to filter
        /// active cart contents (ActiveCartQuery) and should always be false for any row that
        /// still exists.
        /// </summary>
        public bool IsCheckedOut { get; set; }

        [NotMapped]
        public decimal Subtotal => Product != null ? Product.Price * Quantity : 0m;
    }
}