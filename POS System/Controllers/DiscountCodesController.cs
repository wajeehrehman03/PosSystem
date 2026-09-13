using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Discount code management. RESTRICTED: creating, listing, and deactivating discount codes
    /// are SuperAdmin-only management actions, matching CatalogController's convention for
    /// mutating/administrative catalog operations. (Applying an already-known code at checkout
    /// is a Cashier operation and lives in PosController/PosEngine, not here.)
    /// </summary>
    [ApiController]
    [Route("api/discount-codes")]
    [Authorize(Roles = "SuperAdmin")]
    public class DiscountCodesController : ControllerBase
    {
        private readonly DiscountCodeService _discountCodeService;

        public DiscountCodesController(DiscountCodeService discountCodeService)
        {
            _discountCodeService = discountCodeService ?? throw new ArgumentNullException(nameof(discountCodeService));
        }

        /// <summary>
        /// Creates a new discount code.
        /// </summary>
        /// <param name="request">Code, discount type/value, and optional minimum subtotal / expiry</param>
        [HttpPost]
        public IActionResult Create([FromBody] CreateDiscountCodeDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (string.IsNullOrWhiteSpace(request.Code))
                    return BadRequest(new { error = "Code is required" });

                if (string.IsNullOrWhiteSpace(request.Type))
                    return BadRequest(new { error = "Type is required" });

                if (request.Value <= 0)
                    return BadRequest(new { error = "Value must be greater than 0" });

                var discountCode = _discountCodeService.Create(request.Code, request.Type, request.Value, request.MinSubtotal, request.ExpiresAt);
                return CreatedAtAction(nameof(GetByCode), new { code = discountCode.Code }, DiscountCodeResponseDto.FromDiscountCode(discountCode));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create discount code", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves all discount codes.
        /// </summary>
        [HttpGet]
        public IActionResult GetAll()
        {
            try
            {
                var codes = _discountCodeService.GetAll();
                return Ok(new
                {
                    items = codes.Select(DiscountCodeResponseDto.FromDiscountCode),
                    count = codes.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve discount codes", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a discount code by its code (case-insensitive).
        /// </summary>
        /// <param name="code">The discount code</param>
        [HttpGet("{code}")]
        public IActionResult GetByCode(string code)
        {
            try
            {
                var discountCode = _discountCodeService.GetByCode(code);
                if (discountCode == null)
                    return NotFound(new { error = $"Discount code '{code}' not found" });

                return Ok(DiscountCodeResponseDto.FromDiscountCode(discountCode));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve discount code", details = ex.Message });
            }
        }

        /// <summary>
        /// Deactivates a discount code so it can no longer be applied at checkout.
        /// </summary>
        /// <param name="code">The discount code</param>
        [HttpPatch("{code}/deactivate")]
        public IActionResult Deactivate(string code)
        {
            try
            {
                var discountCode = _discountCodeService.GetByCode(code);
                if (discountCode == null)
                    return NotFound(new { error = $"Discount code '{code}' not found" });

                var deactivated = _discountCodeService.Deactivate(discountCode);
                return Ok(DiscountCodeResponseDto.FromDiscountCode(deactivated));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to deactivate discount code", details = ex.Message });
            }
        }
    }
}
