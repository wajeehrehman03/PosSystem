using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Point-of-Sale (POS) system endpoints for transaction processing.
    /// Cart and checkout operations require authentication with Cashier or SuperAdmin role.
    /// Administrative operations are restricted to SuperAdmin users only.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PosController : ControllerBase
    {
        private readonly IPosEngine _posEngine;
        private readonly PaymentService _paymentService;

        public PosController(IPosEngine posEngine, PaymentService paymentService)
        {
            _posEngine = posEngine ?? throw new ArgumentNullException(nameof(posEngine));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        }

        /// <summary>
        /// Retrieves the current shopping cart contents.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <returns>Current cart items and item count</returns>
        [HttpGet("cart")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult GetCart()
        {
            try
            {
                return Ok(new
                {
                    items = _posEngine.GetCart(),
                    itemCount = _posEngine.GetCart().Count()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve cart", details = ex.Message });
            }
        }

        /// <summary>
        /// Adds an item to the shopping cart.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <param name="request">Product ID and quantity to add</param>
        /// <returns>Success message with updated cart info</returns>
        [HttpPost("cart/add")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult AddToCart([FromBody] SaleRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (request.ProductId <= 0)
                    return BadRequest(new { error = "ProductId must be greater than 0" });

                if (request.Quantity <= 0)
                    return BadRequest(new { error = "Quantity must be greater than 0" });

                bool success = _posEngine.AddToCart(request.ProductId, request.Quantity);
                if (!success)
                    return BadRequest(new { error = "Unable to add item to cart. Insufficient stock or invalid Product ID." });

                return Ok(new
                {
                    message = "Item added to cart successfully",
                    cartItemCount = _posEngine.GetCart().Count()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to add item to cart", details = ex.Message });
            }
        }

        /// <summary>
        /// Removes the last added item from the shopping cart (undo operation).
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("cart/undo")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult Undo()
        {
            try
            {
                if (!_posEngine.UndoLastAction())
                    return BadRequest(new { error = "Cart is empty or no action to undo" });

                return Ok(new
                {
                    message = "Last action undone successfully",
                    cartItemCount = _posEngine.GetCart().Count()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to undo last action", details = ex.Message });
            }
        }

        /// <summary>
        /// Removes a single cart line by its id, wherever it sits in the cart - unlike Undo,
        /// which only ever removes the most recently added line.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <param name="id">The CartItem id (as returned by GetCart) to remove</param>
        [HttpDelete("cart/item/{id}")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult RemoveCartItem(int id)
        {
            try
            {
                if (!_posEngine.RemoveFromCart(id))
                    return NotFound(new { error = $"No cart item with id {id} found" });

                return Ok(new
                {
                    message = "Item removed from cart",
                    cartItemCount = _posEngine.GetCart().Count()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to remove item from cart", details = ex.Message });
            }
        }

        /// <summary>
        /// Clears all items from the shopping cart.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("cart/clear")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult ClearCart()
        {
            try
            {
                _posEngine.ClearCart();
                return Ok(new { message = "Cart cleared successfully", cartItemCount = 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to clear cart", details = ex.Message });
            }
        }

        /// <summary>
        /// Processes checkout and creates an order. Supports split-tender payment across
        /// cash/card/gift card. Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <param name="request">Cashier name (optional) and the payment tenders to apply</param>
        /// <returns>Order details (including per-tender breakdown and change due) and receipt</returns>
        [HttpPost("checkout")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                var cart = _posEngine.GetCart();
                if (!cart.Any())
                    return BadRequest(new { error = "Cart is empty. Cannot proceed with checkout" });

                if (request.Tenders == null || request.Tenders.Count == 0)
                    return BadRequest(new { error = "At least one payment tender is required" });

                foreach (var tender in request.Tenders)
                {
                    if (tender.Amount <= 0)
                        return BadRequest(new { error = "Each payment tender amount must be greater than 0" });
                }

                // Authorize card tenders before finalizing the sale - a declined card should
                // never result in a persisted order.
                foreach (var cardTender in request.Tenders.Where(t => string.Equals(t.Method, PaymentMethodConstants.Card, StringComparison.OrdinalIgnoreCase)))
                {
                    bool authorized = await _paymentService.ProcessCardPaymentAsync(cardTender.Amount);
                    if (!authorized)
                        return BadRequest(new { error = $"Card payment of {cardTender.Amount:F2} was declined" });
                }

                var order = _posEngine.Checkout(request.CashierName, request.Tenders, request.CustomerPhone, request.DiscountCode, request.PointsToRedeem ?? 0);

                if (order == null)
                    return StatusCode(500, new { error = "Failed to create order" });

                return Ok(new
                {
                    message = "Checkout successful!",
                    order = new
                    {
                        id = order.Id,
                        orderNumber = order.OrderNumber,
                        subtotal = order.Subtotal,
                        tax = order.TaxAmount,
                        total = order.TotalAmount,
                        cashier = order.ProcessedByCashier,
                        timestamp = order.OrderDate,
                        changeDue = order.ChangeDue,
                        discountAmount = order.DiscountAmount,
                        pointsEarned = order.PointsEarned,
                        pointsRedeemed = order.PointsRedeemed,
                        customerLoyaltyBalance = order.Customer?.LoyaltyPoints,
                        payments = order.Payments.Select(p => new
                        {
                            method = p.Method,
                            amount = p.Amount,
                            referenceNumber = p.ReferenceNumber
                        })
                    },
                    receipt = _posEngine.GenerateReceipt(order),
                    jsonExport = _posEngine.ExportOrderToJson(order)
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Checkout failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets daily sales summary.
        /// RESTRICTED: Only SuperAdmin users can view sales analytics.
        /// </summary>
        /// <returns>Daily sales summary including totals by cashier</returns>
        [HttpGet("daily-summary")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult GetDailySummary()
        {
            try
            {
                // This is a placeholder for actual daily summary calculation
                // In production, this would aggregate orders from the database
                return Ok(new
                {
                    date = DateTime.UtcNow.Date,
                    message = "Daily summary calculation not yet implemented",
                    note = "SuperAdmin-only endpoint for administrative reporting"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve daily summary", details = ex.Message });
            }
        }

        // Shift open/close, cash drops, and X/Z-Report reconciliation now live in
        // ShiftController (api/shifts/*) with real persistence, replacing the placeholder
        // GetShift/CloseShift endpoints that used to live here.
    }
}
