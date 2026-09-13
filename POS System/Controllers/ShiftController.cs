using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Register & shift reconciliation endpoints: opening/closing a shift, recording cash
    /// movements, and pulling live (X-Report) or closing (Z-Report) cash reconciliation reports.
    /// A cashier or SuperAdmin can view/manage their own shift; a SuperAdmin can view/manage any.
    /// </summary>
    [ApiController]
    [Route("api/shifts")]
    [Authorize(Roles = "SuperAdmin,Cashier")]
    public class ShiftController : ControllerBase
    {
        private readonly ShiftService _shiftService;

        public ShiftController(ShiftService shiftService)
        {
            _shiftService = shiftService ?? throw new ArgumentNullException(nameof(shiftService));
        }

        /// <summary>
        /// True if the caller is a SuperAdmin, or is the cashier who owns the shift.
        /// </summary>
        private bool IsSelfOrSuperAdmin(int cashierId)
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != null && roleClaim.Equals(UserRoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                return true;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId) && userId == cashierId;
        }

        /// <summary>
        /// Opens a new shift with a starting cash float.
        /// </summary>
        /// <param name="request">Register identifier and opening cash float</param>
        /// <returns>Created shift with 201 status code; 409 if the caller already has an open shift</returns>
        [HttpPost("open")]
        public IActionResult Open([FromBody] OpenShiftDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (string.IsNullOrWhiteSpace(request.RegisterCode))
                    return BadRequest(new { error = "RegisterCode is required" });

                if (request.OpeningFloat < 0)
                    return BadRequest(new { error = "OpeningFloat cannot be negative" });

                var shift = _shiftService.OpenShift(request.RegisterCode, request.OpeningFloat);
                return CreatedAtAction(nameof(GetById), new { id = shift.Id }, ShiftResponseDto.FromShift(shift));
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
                return StatusCode(500, new { error = "Failed to open shift", details = ex.Message });
            }
        }

        /// <summary>
        /// Records a cash drop to the safe against an open shift.
        /// </summary>
        /// <param name="id">The shift ID</param>
        /// <param name="request">Amount dropped and an optional note</param>
        [HttpPost("{id}/cash-drop")]
        public IActionResult CashDrop(int id, [FromBody] CashDropDto request)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Shift ID must be greater than 0" });

                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                var shift = _shiftService.GetById(id);
                if (shift == null)
                    return NotFound(new { error = $"Shift with ID {id} not found" });

                if (!IsSelfOrSuperAdmin(shift.CashierId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You can only manage your own shift" });

                var movement = _shiftService.RecordCashMovement(shift, CashMovementTypeConstants.CashDrop, request.Amount, request.Note);
                return Ok(new
                {
                    message = "Cash drop recorded",
                    movement = new { movement.Id, movement.ShiftId, movement.Type, movement.Amount, movement.Note, movement.CreatedAt }
                });
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
                return StatusCode(500, new { error = "Failed to record cash drop", details = ex.Message });
            }
        }

        /// <summary>
        /// Live cash reconciliation report (X-Report). Does not close the shift.
        /// </summary>
        /// <param name="id">The shift ID</param>
        [HttpGet("{id}/x-report")]
        public IActionResult XReport(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Shift ID must be greater than 0" });

                var shift = _shiftService.GetById(id);
                if (shift == null)
                    return NotFound(new { error = $"Shift with ID {id} not found" });

                if (!IsSelfOrSuperAdmin(shift.CashierId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You can only view your own shift" });

                return Ok(_shiftService.GenerateReport(shift, isFinal: false));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate X-Report", details = ex.Message });
            }
        }

        /// <summary>
        /// Closes a shift with the cashier's physically counted cash and returns the
        /// closing cash reconciliation report (Z-Report).
        /// </summary>
        /// <param name="id">The shift ID</param>
        /// <param name="request">Cash counted in the drawer at close time</param>
        [HttpPost("{id}/close")]
        public IActionResult Close(int id, [FromBody] CloseShiftDto request)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Shift ID must be greater than 0" });

                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                if (request.ClosingCountedAmount < 0)
                    return BadRequest(new { error = "ClosingCountedAmount cannot be negative" });

                var shift = _shiftService.GetById(id);
                if (shift == null)
                    return NotFound(new { error = $"Shift with ID {id} not found" });

                if (!IsSelfOrSuperAdmin(shift.CashierId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You can only close your own shift" });

                var closedShift = _shiftService.CloseShift(shift, request.ClosingCountedAmount);
                return Ok(_shiftService.GenerateReport(closedShift, isFinal: true));
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
                return StatusCode(500, new { error = "Failed to close shift", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a specific shift by ID.
        /// </summary>
        /// <param name="id">The shift ID</param>
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Shift ID must be greater than 0" });

                var shift = _shiftService.GetById(id);
                if (shift == null)
                    return NotFound(new { error = $"Shift with ID {id} not found" });

                if (!IsSelfOrSuperAdmin(shift.CashierId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You can only view your own shift" });

                return Ok(ShiftResponseDto.FromShift(shift));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve shift", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the sales (orders) rung up during a specific shift, newest first.
        /// </summary>
        /// <param name="id">The shift ID</param>
        [HttpGet("{id}/orders")]
        public IActionResult GetOrders(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Shift ID must be greater than 0" });

                var shift = _shiftService.GetById(id);
                if (shift == null)
                    return NotFound(new { error = $"Shift with ID {id} not found" });

                if (!IsSelfOrSuperAdmin(shift.CashierId))
                    return StatusCode(StatusCodes.Status403Forbidden, new { error = "You can only view your own shift" });

                var orders = _shiftService.GetOrdersForShift(id);
                return Ok(new
                {
                    items = orders.Select(o => OrderResponseDto.FromOrder(o)),
                    count = orders.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve shift orders", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the caller's own currently open shift, if any.
        /// </summary>
        [HttpGet("current")]
        public IActionResult Current()
        {
            try
            {
                var shift = _shiftService.GetCurrentOpenShift();
                if (shift == null)
                    return NotFound(new { error = "No open shift found for the current user" });

                return Ok(ShiftResponseDto.FromShift(shift));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve current shift", details = ex.Message });
            }
        }
    }
}
