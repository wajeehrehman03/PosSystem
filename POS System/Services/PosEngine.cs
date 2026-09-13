using Newtonsoft.Json;
using PosWebApi.Data;
using PosWebApi.Helpers;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace PosWebApi.Services
{
    public class PosEngine : IPosEngine
    {
        private readonly CatalogManager _catalog;
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public PosEngine(CatalogManager catalog, AppDbContext context, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Resolves the authenticated user's ID from the current request's JWT claims.
        /// The cart is persisted per-user in the database (see CartItem.UserId) so that it
        /// survives across requests - PosEngine is registered Scoped, and ASP.NET Core creates
        /// a brand-new DI scope (and therefore a brand-new PosEngine) for every HTTP request.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("No authenticated user found for cart operation");

            return userId;
        }

        private IQueryable<CartItem> ActiveCartQuery(int userId)
        {
            return _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId && !c.IsCheckedOut);
        }

        public bool AddToCart(int productId, int quantity)
        {
            if (productId <= 0 || quantity <= 0)
                return false;

            if (!_catalog.CheckStock(productId, quantity))
                return false;

            var product = _catalog.GetById(productId);
            if (product == null)
                return false;

            var item = new CartItem
            {
                UserId = GetCurrentUserId(),
                ProductId = product.Id,
                Quantity = quantity,
                IsCheckedOut = false
            };

            _context.CartItems.Add(item);
            _context.SaveChanges();
            return true;
        }

        public bool UndoLastAction()
        {
            var userId = GetCurrentUserId();
            var lastItem = ActiveCartQuery(userId).OrderByDescending(c => c.Id).FirstOrDefault();
            if (lastItem == null)
                return false;

            _context.CartItems.Remove(lastItem);
            _context.SaveChanges();
            return true;
        }

        public bool RemoveFromCart(int cartItemId)
        {
            var userId = GetCurrentUserId();
            var item = ActiveCartQuery(userId).FirstOrDefault(c => c.Id == cartItemId);
            if (item == null)
                return false;

            _context.CartItems.Remove(item);
            _context.SaveChanges();
            return true;
        }

        public IEnumerable<CartItem> GetCart()
        {
            var userId = GetCurrentUserId();
            return ActiveCartQuery(userId).ToList().AsReadOnly();
        }

        public void ClearCart()
        {
            var userId = GetCurrentUserId();
            var items = ActiveCartQuery(userId).ToList();
            _context.CartItems.RemoveRange(items);
            _context.SaveChanges();
        }

        /// <summary>
        /// Resolved loyalty/discount inputs for a checkout, computed once before the transaction
        /// so the tender-sufficiency check (which needs the final, discounted total) and the
        /// actual order persistence use identical figures - no risk of the two diverging.
        /// </summary>
        private class CheckoutPricing
        {
            public Customer? Customer;
            public DiscountCode? DiscountCode;
            public int PointsToRedeem;
            public decimal DiscountAmount;
            public decimal TaxableSubtotal;
            public decimal Tax;
            public decimal Total;
        }

        /// <summary>
        /// Resolves the optional customer/discount-code/points-redemption inputs to a checkout
        /// and computes the resulting discount, tax, and total. All failures (unknown customer,
        /// invalid/expired/below-minimum code, insufficient points) throw InvalidOperationException
        /// so PosController's existing catch block maps them to 400, matching every other
        /// checkout validation failure (empty cart, no open shift, invalid tenders).
        /// </summary>
        private CheckoutPricing ResolveCheckoutPricing(decimal cartSubtotal, string? customerPhone, string? discountCode, int pointsToRedeem)
        {
            if (pointsToRedeem < 0)
                throw new InvalidOperationException("PointsToRedeem cannot be negative");

            Customer? customer = null;
            if (!string.IsNullOrWhiteSpace(customerPhone))
            {
                string normalizedPhone;
                try
                {
                    normalizedPhone = CustomerService.NormalizePhone(customerPhone);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException(ex.Message);
                }

                customer = _context.Customers.FirstOrDefault(c => c.Phone == normalizedPhone);
                if (customer == null)
                    throw new InvalidOperationException($"No customer found with phone '{customerPhone}'");
            }

            DiscountCode? resolvedCode = null;
            decimal codeDiscount = 0m;
            if (!string.IsNullOrWhiteSpace(discountCode))
            {
                var normalizedCode = discountCode.Trim().ToLower();
                resolvedCode = _context.DiscountCodes.FirstOrDefault(d => d.Code.ToLower() == normalizedCode);

                if (resolvedCode == null)
                    throw new InvalidOperationException($"Discount code '{discountCode}' not found");

                if (!resolvedCode.IsActive)
                    throw new InvalidOperationException($"Discount code '{discountCode}' is not active");

                if (resolvedCode.ExpiresAt.HasValue && resolvedCode.ExpiresAt.Value < DateTime.UtcNow)
                    throw new InvalidOperationException($"Discount code '{discountCode}' has expired");

                if (resolvedCode.MinSubtotal.HasValue && cartSubtotal < resolvedCode.MinSubtotal.Value)
                    throw new InvalidOperationException($"Discount code '{discountCode}' requires a minimum subtotal of {resolvedCode.MinSubtotal.Value:F2}");

                codeDiscount = resolvedCode.Type == DiscountCodeTypeConstants.Percent
                    ? cartSubtotal * (resolvedCode.Value / 100m)
                    : resolvedCode.Value;
            }

            decimal pointsDiscount = 0m;
            if (pointsToRedeem > 0)
            {
                if (customer == null)
                    throw new InvalidOperationException("Redeeming points requires a customer - provide CustomerPhone");

                if (customer.LoyaltyPoints < pointsToRedeem)
                    throw new InvalidOperationException($"Customer has insufficient loyalty points. Available: {customer.LoyaltyPoints}, Requested: {pointsToRedeem}");

                var redemptionRate = decimal.Parse(_configuration["Loyalty:PointsRedemptionRate"] ?? "100");
                pointsDiscount = pointsToRedeem / redemptionRate;
            }

            // A fixed-amount code plus a large points redemption could otherwise exceed the
            // subtotal - clamp the combined discount rather than let the sale go negative.
            decimal discountAmount = Math.Min(codeDiscount + pointsDiscount, cartSubtotal);
            decimal taxableSubtotal = cartSubtotal - discountAmount;
            decimal tax = TaxHelper.CalculateTax(taxableSubtotal);

            return new CheckoutPricing
            {
                Customer = customer,
                DiscountCode = resolvedCode,
                PointsToRedeem = pointsToRedeem,
                DiscountAmount = discountAmount,
                TaxableSubtotal = taxableSubtotal,
                Tax = tax,
                Total = taxableSubtotal + tax
            };
        }

        public Order Checkout(string? cashierName, List<PaymentRequestDto> tenders, string? customerPhone = null, string? discountCode = null, int pointsToRedeem = 0)
        {
            var userId = GetCurrentUserId();
            var cartItems = ActiveCartQuery(userId).ToList();

            if (cartItems.Count == 0)
                throw new InvalidOperationException("Cannot checkout with an empty cart");

            var lineItems = cartItems
                .Select(c => new SaleLineItem { ProductId = c.ProductId, Quantity = c.Quantity })
                .ToList();

            // The cart rows are no longer needed once their contents are captured as OrderItem
            // snapshots - remove them rather than leaving orphaned "checked out" rows with no
            // further purpose. Passed as a callback so ProcessSaleCore can run it inside its own
            // transaction, right before commit, instead of a second SaveChanges after that
            // transaction has already committed - a crash in that narrow window used to be able
            // to leave stale (harmless, but untidy) cart rows behind.
            var order = ProcessSaleCore(userId, lineItems, cashierName, tenders, customerPhone, discountCode, pointsToRedeem,
                occurredAtOverride: null, clientTransactionId: null,
                preCommitCleanup: () => _context.CartItems.RemoveRange(cartItems));

            return order;
        }

        public Order ProcessQueuedSale(string clientTransactionId, DateTime occurredAt, string? cashierName, List<SaleLineItem> items, List<PaymentRequestDto> tenders, string? customerPhone = null, string? discountCode = null, int pointsToRedeem = 0)
        {
            if (string.IsNullOrWhiteSpace(clientTransactionId))
                throw new InvalidOperationException("ClientTransactionId is required");

            var userId = GetCurrentUserId();
            return ProcessSaleCore(userId, items, cashierName, tenders, customerPhone, discountCode, pointsToRedeem, occurredAtOverride: occurredAt, clientTransactionId: clientTransactionId);
        }

        public Order? FindOrderByClientTransactionId(string clientTransactionId)
        {
            if (string.IsNullOrWhiteSpace(clientTransactionId))
                return null;

            return _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Include(o => o.Customer)
                .Include(o => o.DiscountCode)
                .FirstOrDefault(o => o.ClientTransactionId == clientTransactionId);
        }

        /// <summary>
        /// The shared core of every sale: resolves the caller's open shift, prices the sale
        /// (subtotal/discount/tax/total via ResolveCheckoutPricing), validates tenders, deducts
        /// stock, and persists the Order/OrderItem snapshots/Payment rows/LoyaltyTransaction rows
        /// in one transaction. Used by both Checkout (items sourced from the persisted cart) and
        /// ProcessQueuedSale (items submitted directly by an offline-queued sale replay) so the
        /// two can never drift apart in pricing/validation/persistence behavior.
        ///
        /// occurredAtOverride sets Order.OrderDate explicitly (for a queued sale, the time it
        /// actually happened offline); when null, it defaults to DateTime.UtcNow as it always did.
        /// clientTransactionId stamps the offline idempotency key; null for ordinary online sales.
        ///
        /// preCommitCleanup, if supplied, runs just before the transaction commits (e.g. Checkout
        /// uses it to remove the now-redundant cart rows in the same transaction as the order
        /// they were captured into, rather than a separate SaveChanges after the fact).
        ///
        /// Does not itself check for a duplicate ClientTransactionId - callers processing queued
        /// sales must call FindOrderByClientTransactionId first (see ProcessQueuedSale's contract).
        /// </summary>
        private Order ProcessSaleCore(int userId, List<SaleLineItem> items, string? cashierName, List<PaymentRequestDto> tenders, string? customerPhone, string? discountCode, int pointsToRedeem, DateTime? occurredAtOverride, string? clientTransactionId, Action? preCommitCleanup = null)
        {
            if (items == null || items.Count == 0)
                throw new InvalidOperationException("Cannot checkout with no items");

            if (tenders == null || tenders.Count == 0)
                throw new InvalidOperationException("At least one payment tender is required");

            foreach (var tender in tenders)
            {
                if (!PaymentMethodConstants.IsValidMethod(tender.Method))
                    throw new InvalidOperationException($"Invalid payment method '{tender.Method}'. Valid methods are: {string.Join(", ", PaymentMethodConstants.AllMethods)}");

                if (tender.Amount <= 0)
                    throw new InvalidOperationException("Each payment tender amount must be greater than 0");
            }

            // Checkout requires an open shift so the sale is attributed to a register for
            // cash reconciliation (X/Z-Reports). Checked outside the transaction, same as the
            // empty-items check above, so the error message isn't swallowed by the generic
            // "Checkout failed" wrapper in the catch block below.
            var openShift = _context.Shifts.FirstOrDefault(s => s.CashierId == userId && s.Status == ShiftStatusConstants.Open);
            if (openShift == null)
                throw new InvalidOperationException("Cannot checkout without an open shift. Open a shift first.");

            // Resolve products up front for the pricing preview. Re-resolved from the same
            // CatalogManager.GetById source again inside the transaction below (prices could in
            // theory change between the preview and the transaction under concurrent load) so the
            // two can never diverge in a way that under-charges or double-charges.
            decimal cartTotalPreview = 0m;
            foreach (var line in items)
            {
                if (line.Quantity <= 0)
                    throw new InvalidOperationException($"Quantity for product {line.ProductId} must be greater than 0");

                var previewProduct = _catalog.GetById(line.ProductId);
                if (previewProduct == null)
                    throw new InvalidOperationException($"Product with ID {line.ProductId} not found");

                cartTotalPreview += previewProduct.Price * line.Quantity;
            }

            var pricing = ResolveCheckoutPricing(cartTotalPreview, customerPhone, discountCode, pointsToRedeem);
            decimal orderTotalPreview = pricing.Total;

            // Only cash can produce change - a card or gift card tender that alone exceeds the
            // total makes no sense (you can't partially refund a card authorization), so that's
            // rejected outright rather than silently treated as an overpayment.
            decimal nonCashTotal = tenders
                .Where(t => !t.Method.Equals(PaymentMethodConstants.Cash, StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
            decimal totalTendered = tenders.Sum(t => t.Amount);

            if (nonCashTotal > orderTotalPreview)
                throw new InvalidOperationException("Card or gift card payment cannot exceed the order total. Only cash can produce change.");

            if (totalTendered < orderTotalPreview)
                throw new InvalidOperationException($"Insufficient payment. Order total is {orderTotalPreview:F2} but only {totalTendered:F2} was tendered.");

            // Since nonCashTotal <= orderTotalPreview (checked above), any excess beyond the
            // total is necessarily covered by the cash portion.
            decimal changeDue = totalTendered - orderTotalPreview;

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    decimal subtotal = 0m;
                    var orderItems = new List<OrderItem>();
                    foreach (var line in items)
                    {
                        var product = _catalog.GetById(line.ProductId);
                        if (product == null)
                            throw new InvalidOperationException($"Product with ID {line.ProductId} not found");

                        // Snapshot the product's name/price now, at sale time, rather than
                        // keeping a live reference - a later price change or product deletion
                        // must not alter historical order/receipt data.
                        decimal unitPrice = product.Price;
                        subtotal += unitPrice * line.Quantity;

                        _catalog.DeductStock(line.ProductId, line.Quantity);

                        orderItems.Add(new OrderItem
                        {
                            ProductId = line.ProductId,
                            ProductNameSnapshot = product.Name,
                            Quantity = line.Quantity,
                            UnitPriceSnapshot = unitPrice,
                            LineTotal = unitPrice * line.Quantity
                        });
                    }

                    // Recomputed from the authoritative loop-summed subtotal (rather than reusing
                    // pricing.Tax/Total directly) the same way the pre-discount code always did -
                    // only the already-resolved customer/discount-code entities and the clamped
                    // DiscountAmount are carried over from ResolveCheckoutPricing, since re-running
                    // that validation here would just repeat the same DB queries for the same result.
                    decimal discountAmount = pricing.DiscountAmount;
                    decimal taxableSubtotal = subtotal - discountAmount;
                    decimal tax = TaxHelper.CalculateTax(taxableSubtotal);
                    decimal totalAmount = taxableSubtotal + tax;

                    // Points are earned on what the customer actually paid (the final,
                    // post-discount, post-tax total), not the pre-discount price. Rounded down
                    // to whole points.
                    int pointsEarned = 0;
                    if (pricing.Customer != null)
                    {
                        var earnRate = decimal.Parse(_configuration["Loyalty:PointsEarnedPerDollar"] ?? "1");
                        pointsEarned = (int)Math.Floor(totalAmount * earnRate);
                    }

                    var order = new Order
                    {
                        Items = orderItems,
                        Subtotal = subtotal,
                        DiscountAmount = discountAmount,
                        TaxAmount = tax,
                        TotalAmount = totalAmount,
                        ChangeDue = changeDue,
                        ProcessedByCashier = string.IsNullOrWhiteSpace(cashierName) ? "Default Cashier" : cashierName,
                        OrderDate = occurredAtOverride ?? DateTime.UtcNow,
                        ShiftId = openShift.Id,
                        ClientTransactionId = clientTransactionId,
                        Customer = pricing.Customer,
                        DiscountCode = pricing.DiscountCode,
                        PointsEarned = pointsEarned,
                        PointsRedeemed = pricing.PointsToRedeem,
                        Payments = tenders.Select(t => new Payment
                        {
                            Method = t.Method,
                            Amount = t.Amount,
                            ReferenceNumber = t.ReferenceNumber
                        }).ToList()
                    };

                    _context.Orders.Add(order);
                    _context.SaveChanges();

                    if (pricing.Customer != null)
                    {
                        if (pricing.PointsToRedeem > 0)
                        {
                            pricing.Customer.LoyaltyPoints -= pricing.PointsToRedeem;
                            _context.LoyaltyTransactions.Add(new LoyaltyTransaction
                            {
                                CustomerId = pricing.Customer.Id,
                                OrderId = order.Id,
                                Type = LoyaltyTransactionTypeConstants.Redeem,
                                Points = pricing.PointsToRedeem,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        if (pointsEarned > 0)
                        {
                            pricing.Customer.LoyaltyPoints += pointsEarned;
                            _context.LoyaltyTransactions.Add(new LoyaltyTransaction
                            {
                                CustomerId = pricing.Customer.Id,
                                OrderId = order.Id,
                                Type = LoyaltyTransactionTypeConstants.Earn,
                                Points = pointsEarned,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        _context.Customers.Update(pricing.Customer);
                        _context.SaveChanges();
                    }

                    if (preCommitCleanup != null)
                    {
                        preCommitCleanup();
                        _context.SaveChanges();
                    }

                    transaction.Commit();
                    return order;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new InvalidOperationException("Checkout failed. Transaction rolled back.", ex);
                }
            }
        }

        public string GenerateReceipt(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var sb = new StringBuilder();
            sb.AppendLine("=== POS SALES RECEIPT ===");
            sb.AppendLine($"Order Number: {order.OrderNumber}");
            sb.AppendLine($"Cashier: {order.ProcessedByCashier}");
            sb.AppendLine($"Date: {order.OrderDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("-------------------------");

            if (order.Items != null && order.Items.Count > 0)
            {
                foreach (var item in order.Items)
                {
                    sb.AppendLine($"{item.ProductNameSnapshot} x{item.Quantity} @ ${item.UnitPriceSnapshot:F2} = ${item.LineTotal:F2}");
                }
            }
            else
            {
                sb.AppendLine("(No items in order)");
            }

            sb.AppendLine("-------------------------");
            sb.AppendLine($"Subtotal: ${order.Subtotal:F2}");
            sb.AppendLine($"Tax (15%): ${order.TaxAmount:F2}");
            sb.AppendLine($"Grand Total: ${order.TotalAmount:F2}");
            sb.AppendLine("=========================");

            return sb.ToString();
        }

        public string ExportOrderToJson(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            // ReferenceLoopHandling.Ignore matches the global AddNewtonsoftJson() configuration
            // in Program.cs (used for MVC action results). This call bypasses that pipeline by
            // invoking JsonConvert directly, so it needs the same setting explicitly - otherwise
            // Order.Payments <-> Payment.Order (and Order.Items[].Product, etc.) trips
            // Newtonsoft's default circular-reference detection.
            var settings = new Newtonsoft.Json.JsonSerializerSettings
            {
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                Formatting = Newtonsoft.Json.Formatting.Indented
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(order, settings);
        }
    }
}
