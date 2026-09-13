using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Replays offline-queued sales once a register regains connectivity. Each entry in a batch
    /// is processed independently - the endpoint always returns 200, with the actual per-entry
    /// outcome (Created/AlreadyProcessed/Failed) carried in the response body, so one bad entry
    /// (insufficient stock, no open shift, etc.) never blocks the rest of the batch.
    /// </summary>
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly IPosEngine _posEngine;

        public SyncController(IPosEngine posEngine)
        {
            _posEngine = posEngine ?? throw new ArgumentNullException(nameof(posEngine));
        }

        /// <summary>
        /// Replays a batch of offline-queued sales. Requires authentication with Cashier or
        /// SuperAdmin role - the authenticated caller is who the sales are attributed to (their
        /// currently-open shift, at sync time).
        /// </summary>
        [HttpPost("sales")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult SyncSales([FromBody] SyncSalesRequestDto request)
        {
            if (request == null || request.Sales == null)
                return BadRequest(new { error = "Request body is required" });

            var results = new List<SyncSaleResultDto>();

            foreach (var sale in request.Sales)
            {
                if (string.IsNullOrWhiteSpace(sale.ClientTransactionId))
                {
                    results.Add(new SyncSaleResultDto
                    {
                        ClientTransactionId = sale.ClientTransactionId ?? string.Empty,
                        Status = SyncSaleStatusConstants.Failed,
                        ErrorMessage = "ClientTransactionId is required"
                    });
                    continue;
                }

                var existing = _posEngine.FindOrderByClientTransactionId(sale.ClientTransactionId);
                if (existing != null)
                {
                    results.Add(new SyncSaleResultDto
                    {
                        ClientTransactionId = sale.ClientTransactionId,
                        Status = SyncSaleStatusConstants.AlreadyProcessed,
                        OrderId = existing.Id
                    });
                    continue;
                }

                try
                {
                    var items = sale.Items?
                        .Select(i => new SaleLineItem { ProductId = i.ProductId, Quantity = i.Quantity })
                        .ToList() ?? new List<SaleLineItem>();

                    var order = _posEngine.ProcessQueuedSale(
                        sale.ClientTransactionId,
                        sale.OccurredAt,
                        sale.CashierName,
                        items,
                        sale.Tenders,
                        sale.CustomerPhone,
                        sale.DiscountCode,
                        sale.PointsToRedeem ?? 0);

                    results.Add(new SyncSaleResultDto
                    {
                        ClientTransactionId = sale.ClientTransactionId,
                        Status = SyncSaleStatusConstants.Created,
                        OrderId = order.Id
                    });
                }
                catch (Exception ex)
                {
                    // Surface the real, specific reason (e.g. "Insufficient stock for product 3.
                    // Available: 2, Requested: 5") rather than the generic outer wrapper message,
                    // so the register app has enough detail for manual reconciliation.
                    var detail = ex.InnerException?.Message ?? ex.Message;
                    results.Add(new SyncSaleResultDto
                    {
                        ClientTransactionId = sale.ClientTransactionId,
                        Status = SyncSaleStatusConstants.Failed,
                        ErrorMessage = detail
                    });
                }
            }

            return Ok(new SyncSalesResponseDto { Results = results });
        }
    }
}
