using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    /// <summary>One requested return line: which OrderItem, and how much of it.</summary>
    public class RefundItemRequest
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Processes returns against a completed Order. Supports partial, line-item-level refunds,
    /// tracked so the same OrderItem can be refunded across multiple separate returns without
    /// ever exceeding the quantity originally sold. Restocks the returned quantity and, for
    /// orders attributed to a loyalty customer, reverses a proportional share of points earned.
    /// </summary>
    public class RefundService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ShiftService _shiftService;
        private readonly CatalogManager _catalog;

        public RefundService(AppDbContext context, IHttpContextAccessor httpContextAccessor, ShiftService shiftService, CatalogManager catalog)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _shiftService = shiftService ?? throw new ArgumentNullException(nameof(shiftService));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        private int GetCurrentUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !int.TryParse(claim.Value, out int userId))
                throw new UnauthorizedAccessException("No authenticated user found for refund operation");

            return userId;
        }

        /// <summary>Quantity of an OrderItem already refunded across all prior refunds.</summary>
        private int GetAlreadyRefundedQuantity(int orderItemId)
        {
            return _context.RefundItems
                .Where(ri => ri.OrderItemId == orderItemId)
                .Sum(ri => (int?)ri.Quantity) ?? 0;
        }

        public Refund ProcessRefund(int orderId, List<RefundItemRequest> items, string method, string? reason, string? cashierName)
        {
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("At least one item is required to process a refund");

            if (!PaymentMethodConstants.IsValidMethod(method))
                throw new InvalidOperationException($"Invalid refund method '{method}'. Valid methods are: {string.Join(", ", PaymentMethodConstants.AllMethods)}");

            var order = _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
                throw new InvalidOperationException($"Order with ID {orderId} not found");

            var userId = GetCurrentUserId();
            var openShift = _shiftService.GetOpenShiftForCashier(userId);
            if (openShift == null)
                throw new InvalidOperationException("Cannot process a refund without an open shift. Open a shift first.");

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var refundItems = new List<RefundItem>();
                decimal refundSubtotal = 0m;

                foreach (var request in items)
                {
                    if (request.Quantity <= 0)
                        throw new InvalidOperationException("Refund quantity must be greater than 0");

                    var orderItem = order.Items.FirstOrDefault(oi => oi.Id == request.OrderItemId);
                    if (orderItem == null)
                        throw new InvalidOperationException($"Order item {request.OrderItemId} does not belong to order {orderId}");

                    var alreadyRefunded = GetAlreadyRefundedQuantity(orderItem.Id);
                    var refundable = orderItem.Quantity - alreadyRefunded;
                    if (request.Quantity > refundable)
                        throw new InvalidOperationException($"Cannot refund {request.Quantity} of '{orderItem.ProductNameSnapshot}' - only {refundable} remaining refundable");

                    var lineTotal = orderItem.UnitPriceSnapshot * request.Quantity;
                    refundSubtotal += lineTotal;

                    refundItems.Add(new RefundItem
                    {
                        OrderItemId = orderItem.Id,
                        ProductId = orderItem.ProductId,
                        ProductNameSnapshot = orderItem.ProductNameSnapshot,
                        Quantity = request.Quantity,
                        UnitPriceSnapshot = orderItem.UnitPriceSnapshot,
                        LineTotal = lineTotal
                    });

                    // Restock via CatalogManager (not a direct Product mutation) so the Redis
                    // catalog cache is invalidated the same way DeductStock invalidates it at
                    // checkout - otherwise a cached GET /api/catalog read would stay stale.
                    _catalog.RestockProduct(orderItem.ProductId, request.Quantity);
                }

                // Prorate tax/discount by the fraction of the original subtotal being returned,
                // rather than re-deriving a tax rate - keeps a partial refund consistent with
                // however the original sale was actually priced (including any discount applied).
                decimal proportion = order.Subtotal > 0 ? refundSubtotal / order.Subtotal : 0m;
                decimal taxRefunded = Math.Round(order.TaxAmount * proportion, 2);
                decimal discountRefunded = Math.Round(order.DiscountAmount * proportion, 2);
                decimal totalRefunded = refundSubtotal - discountRefunded + taxRefunded;

                var refund = new Refund
                {
                    OrderId = order.Id,
                    ShiftId = openShift.Id,
                    Items = refundItems,
                    Method = method,
                    Reason = reason,
                    SubtotalRefunded = refundSubtotal,
                    TaxRefunded = taxRefunded,
                    TotalRefunded = totalRefunded,
                    ProcessedByCashier = string.IsNullOrWhiteSpace(cashierName) ? "Default Cashier" : cashierName,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Refunds.Add(refund);

                // Reverse a proportional share of loyalty points earned on the original sale.
                // Redeemed points are NOT restored - a spent point isn't refundable, matching
                // how PointsEarned/PointsRedeemed are already treated as independent in Checkout.
                if (order.Customer != null && order.PointsEarned > 0)
                {
                    var pointsToReverse = (int)Math.Floor(order.PointsEarned * proportion);
                    if (pointsToReverse > 0)
                    {
                        pointsToReverse = Math.Min(pointsToReverse, order.Customer.LoyaltyPoints);
                        order.Customer.LoyaltyPoints -= pointsToReverse;
                        _context.LoyaltyTransactions.Add(new LoyaltyTransaction
                        {
                            CustomerId = order.Customer.Id,
                            OrderId = order.Id,
                            Type = LoyaltyTransactionTypeConstants.Refund,
                            Points = pointsToReverse,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                _context.SaveChanges();
                transaction.Commit();
                return refund;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<Refund> GetRefundsForOrder(int orderId)
        {
            return _context.Refunds
                .Include(r => r.Items)
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }
    }
}
