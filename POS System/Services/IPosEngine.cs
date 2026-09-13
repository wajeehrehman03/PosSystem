using System.Collections.Generic;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;

namespace PosWebApi.Services
{
    public interface IPosEngine
    {
        bool AddToCart(int productId, int quantity);
        bool UndoLastAction();

        /// <summary>
        /// Removes one specific cart line (by CartItem.Id) regardless of its position, so the
        /// cashier can drop a single misadded item without having to Undo everything added after
        /// it. Scoped to the caller's own cart - returns false (not found) for another user's
        /// item or an already-checked-out row, the same "no-op on a bad id" contract as
        /// UndoLastAction returning false on an empty cart.
        /// </summary>
        bool RemoveFromCart(int cartItemId);

        IEnumerable<CartItem> GetCart();
        void ClearCart();

        /// <summary>
        /// Checks out the current cart against one or more payment tenders. Tenders must sum
        /// to at least the order total; card/gift card tenders can never exceed it (only cash
        /// can produce change). Throws InvalidOperationException for an empty cart, no open
        /// shift, invalid tenders, or insufficient/invalid payment.
        ///
        /// customerPhone/discountCode/pointsToRedeem are all optional - an anonymous sale with
        /// none of them behaves exactly as before loyalty/promotions existed. Providing
        /// customerPhone attributes the sale to that loyalty customer and earns them points on
        /// the post-discount total; discountCode and/or pointsToRedeem apply a discount (summed
        /// together, clamped so it can never exceed the cart subtotal) before tax is calculated.
        /// Throws InvalidOperationException for an unknown customer/code, an inactive/expired/
        /// below-minimum code, or insufficient points.
        /// </summary>
        Order Checkout(string? cashierName, List<PaymentRequestDto> tenders, string? customerPhone = null, string? discountCode = null, int pointsToRedeem = 0);

        /// <summary>
        /// Replays a single offline-queued sale (see SyncService/SyncController) through the same
        /// pricing/tax/discount/loyalty/stock-deduction/payment logic as Checkout, but sourced
        /// from explicit line items rather than the caller's persisted cart, and stamped with the
        /// submitted ClientTransactionId and OccurredAt rather than "now". Attributed to whatever
        /// shift is currently open for the caller at sync time (not reconstructed from when the
        /// sale actually happened offline). Throws InvalidOperationException for the same reasons
        /// as Checkout (no open shift, insufficient stock, invalid/insufficient tenders, unknown
        /// customer/discount code, etc.) - the caller is expected to catch this per-entry so one
        /// bad queued sale doesn't abort the rest of a batch.
        ///
        /// Callers MUST check FindOrderByClientTransactionId first - this method does not itself
        /// perform the idempotency check, so calling it twice with the same ClientTransactionId
        /// creates two orders.
        /// </summary>
        Order ProcessQueuedSale(string clientTransactionId, DateTime occurredAt, string? cashierName, List<SaleLineItem> items, List<PaymentRequestDto> tenders, string? customerPhone = null, string? discountCode = null, int pointsToRedeem = 0);

        /// <summary>Looks up a previously-synced order by its offline idempotency key, or null if none exists yet.</summary>
        Order? FindOrderByClientTransactionId(string clientTransactionId);

        string GenerateReceipt(Order order);
        string ExportOrderToJson(Order order);
    }
}