using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Order lookup and receipt generation for completed sales. Distinct from PosController,
    /// which owns the active cart/checkout flow - this controller only reads already-persisted
    /// orders. Any authenticated staff member (Cashier or SuperAdmin) can look up an order or
    /// reprint its receipt; these are routine register operations, not restricted financial data
    /// (unlike shift X/Z-Reports).
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    [Authorize(Roles = "SuperAdmin,Cashier")]
    public class OrdersController : ControllerBase
    {
        private readonly ReceiptService receiptService;
        private readonly ShiftService _shiftService;
        private readonly RefundService _refundService;

        public OrdersController(ReceiptService receiptService, ShiftService shiftService, RefundService refundService)
        {
            this.receiptService = receiptService ?? throw new ArgumentNullException(nameof(receiptService));
            _shiftService = shiftService ?? throw new ArgumentNullException(nameof(shiftService));
            _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        }

        /// <summary>
        /// Retrieves every sale a specific cashier has rung up, across all their shifts.
        /// RESTRICTED: SuperAdmin only.
        /// </summary>
        /// <param name="cashierId">The cashier's user ID</param>
        [HttpGet("by-cashier/{cashierId}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult GetByCashier(int cashierId)
        {
            try
            {
                if (cashierId <= 0)
                    return BadRequest(new { error = "Cashier ID must be greater than 0" });

                var orders = _shiftService.GetOrdersForCashier(cashierId);
                return Ok(new
                {
                    items = orders.Select(o => OrderResponseDto.FromOrder(o)),
                    count = orders.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve cashier sales", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a completed order's full details (line items, payments, shift).
        /// </summary>
        /// <param name="id">The order ID</param>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Order ID must be greater than 0" });

                var order = receiptService.GetOrderWithDetails(id);
                if (order == null)
                    return NotFound(new { error = $"Order with ID {id} not found" });

                var refundedByOrderItemId = _refundService.GetRefundsForOrder(id)
                    .SelectMany(r => r.Items)
                    .GroupBy(ri => ri.OrderItemId)
                    .ToDictionary(g => g.Key, g => g.Sum(ri => ri.Quantity));

                return Ok(OrderResponseDto.FromOrder(order, refundedByOrderItemId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve order", details = ex.Message });
            }
        }

        /// <summary>
        /// Processes a return against a completed order - full or partial, by line item.
        /// Requires an open shift, same as a sale, since the refund is charged against the drawer.
        /// </summary>
        /// <param name="id">The order ID being refunded against</param>
        /// <param name="request">Which line items (and quantities) to return, and how to pay it out</param>
        [HttpPost("{id}/refund")]
        public IActionResult Refund(int id, [FromBody] RefundRequestDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (request.Items == null || request.Items.Count == 0)
                    return BadRequest(new { error = "At least one item is required" });

                var items = request.Items
                    .Select(i => new RefundItemRequest { OrderItemId = i.OrderItemId, Quantity = i.Quantity })
                    .ToList();

                var refund = _refundService.ProcessRefund(id, items, request.Method, request.Reason, request.CashierName);
                return Ok(RefundResponseDto.FromRefund(refund));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Refund failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Generates and returns a printable receipt for a completed order.
        /// </summary>
        /// <param name="id">The order ID</param>
        /// <param name="format">"pdf" (default) or "escpos"</param>
        [HttpGet("{id}/receipt")]
        public IActionResult GetReceipt(int id, [FromQuery] string format = "pdf")
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Order ID must be greater than 0" });

                var normalizedFormat = format.Trim().ToLowerInvariant();
                if (normalizedFormat != "pdf" && normalizedFormat != "escpos")
                    return BadRequest(new { error = "Unsupported format. Valid values are: pdf, escpos" });

                var order = receiptService.GetOrderWithDetails(id);
                if (order == null)
                    return NotFound(new { error = $"Order with ID {id} not found" });

                if (normalizedFormat == "pdf")
                {
                    var pdfBytes = receiptService.GeneratePdf(order);
                    return File(pdfBytes, "application/pdf", $"receipt-{order.OrderNumber}.pdf");
                }

                var escPosBytes = receiptService.GenerateEscPos(order);
                return File(escPosBytes, "application/octet-stream", $"receipt-{order.OrderNumber}.bin");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate receipt", details = ex.Message });
            }
        }
    }
}
