using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Register/terminal management. Listing active registers is available to any authenticated
    /// staff member (a cashier needs to pick one when opening a shift); creating/deactivating is
    /// SuperAdmin-only, matching DiscountCodesController's convention for management actions.
    /// </summary>
    [ApiController]
    [Route("api/registers")]
    [Authorize(Roles = "SuperAdmin,Cashier")]
    public class RegistersController : ControllerBase
    {
        private readonly RegisterService _registerService;

        public RegistersController(RegisterService registerService)
        {
            _registerService = registerService ?? throw new ArgumentNullException(nameof(registerService));
        }

        /// <summary>Lists every register, active or not. RESTRICTED: SuperAdmin only.</summary>
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult GetAll()
        {
            try
            {
                var registers = _registerService.GetAll();
                return Ok(new { items = registers.Select(RegisterResponseDto.FromRegister), count = registers.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve registers", details = ex.Message });
            }
        }

        /// <summary>Lists only active registers - for the "pick a register" step of opening a shift.</summary>
        [HttpGet("active")]
        public IActionResult GetActive()
        {
            try
            {
                var registers = _registerService.GetAllActive();
                return Ok(new { items = registers.Select(RegisterResponseDto.FromRegister), count = registers.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve active registers", details = ex.Message });
            }
        }

        /// <summary>Creates a new register. RESTRICTED: SuperAdmin only.</summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Create([FromBody] CreateRegisterDto request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                var register = _registerService.Create(request.Code, request.Name);
                return CreatedAtAction(nameof(GetAll), RegisterResponseDto.FromRegister(register));
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
                return StatusCode(500, new { error = "Failed to create register", details = ex.Message });
            }
        }

        /// <summary>Deactivates a register so it can no longer be used to open a new shift. RESTRICTED: SuperAdmin only.</summary>
        [HttpPatch("{code}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Deactivate(string code)
        {
            try
            {
                var register = _registerService.GetByCode(code);
                if (register == null)
                    return NotFound(new { error = $"Register '{code}' not found" });

                var deactivated = _registerService.Deactivate(register);
                return Ok(RegisterResponseDto.FromRegister(deactivated));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to deactivate register", details = ex.Message });
            }
        }

        /// <summary>Reactivates a previously deactivated register so it can be used to open a new shift again. RESTRICTED: SuperAdmin only.</summary>
        [HttpPatch("{code}/activate")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Activate(string code)
        {
            try
            {
                var register = _registerService.GetByCode(code);
                if (register == null)
                    return NotFound(new { error = $"Register '{code}' not found" });

                var activated = _registerService.Activate(register);
                return Ok(RegisterResponseDto.FromRegister(activated));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to activate register", details = ex.Message });
            }
        }
    }
}
