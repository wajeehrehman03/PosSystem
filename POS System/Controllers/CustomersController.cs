using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Loyalty customer registration and lookup. Any authenticated staff member (Cashier or
    /// SuperAdmin) can register a customer or pull up their loyalty balance/history at the
    /// register - these are routine operations, not restricted management actions.
    /// </summary>
    [ApiController]
    [Route("api/customers")]
    [Authorize(Roles = "SuperAdmin,Cashier")]
    public class CustomersController : ControllerBase
    {
        private readonly CustomerService _customerService;

        public CustomersController(CustomerService customerService)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        }

        /// <summary>
        /// Registers a new loyalty customer.
        /// </summary>
        /// <param name="request">Phone number (the lookup key) and display name</param>
        [HttpPost]
        public IActionResult Register([FromBody] RegisterCustomerDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (string.IsNullOrWhiteSpace(request.Phone))
                    return BadRequest(new { error = "Phone is required" });

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { error = "Name is required" });

                var customer = _customerService.Register(request.Phone, request.Name);
                return CreatedAtAction(nameof(GetByPhone), new { phone = customer.Phone }, CustomerResponseDto.FromCustomer(customer));
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
                return StatusCode(500, new { error = "Failed to register customer", details = ex.Message });
            }
        }

        /// <summary>
        /// Looks up a loyalty customer by phone number.
        /// </summary>
        /// <param name="phone">The customer's phone number (any common formatting is accepted)</param>
        [HttpGet("{phone}")]
        public IActionResult GetByPhone(string phone)
        {
            try
            {
                var customer = _customerService.GetByPhone(phone);
                if (customer == null)
                    return NotFound(new { error = $"No customer found with phone '{phone}'" });

                return Ok(CustomerResponseDto.FromCustomer(customer));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve customer", details = ex.Message });
            }
        }

        /// <summary>
        /// Manually credits or debits a customer's loyalty balance outside of any sale (goodwill
        /// points, correcting a mistake, etc). RESTRICTED to SuperAdmin - unlike registration/
        /// lookup, granting free points is a management action, not a routine register operation.
        /// </summary>
        /// <param name="phone">The customer's phone number</param>
        /// <param name="request">Points delta (positive credits, negative debits) and an optional reason</param>
        [HttpPost("{phone}/loyalty-adjustment")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult AdjustLoyaltyPoints(string phone, [FromBody] AdjustLoyaltyPointsDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (request.Points == 0)
                    return BadRequest(new { error = "Points must be non-zero" });

                var customer = _customerService.GetByPhone(phone);
                if (customer == null)
                    return NotFound(new { error = $"No customer found with phone '{phone}'" });

                var updated = _customerService.AdjustLoyaltyPoints(customer.Id, request.Points, request.Reason);
                return Ok(CustomerResponseDto.FromCustomer(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to adjust loyalty points", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a customer's loyalty points ledger (Earn/Redeem history), newest first.
        /// </summary>
        /// <param name="phone">The customer's phone number</param>
        [HttpGet("{phone}/loyalty-history")]
        public IActionResult GetLoyaltyHistory(string phone)
        {
            try
            {
                var customer = _customerService.GetByPhone(phone);
                if (customer == null)
                    return NotFound(new { error = $"No customer found with phone '{phone}'" });

                var history = _customerService.GetLoyaltyHistory(customer.Id);
                return Ok(new
                {
                    items = history.Select(LoyaltyTransactionResponseDto.FromTransaction),
                    count = history.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve loyalty history", details = ex.Message });
            }
        }
    }
}
