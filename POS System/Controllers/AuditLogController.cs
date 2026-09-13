using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosWebApi.Models.Dtos;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Read-only audit trail of administrative actions (product changes, user creation).
    /// RESTRICTED: SuperAdmin only.
    /// </summary>
    [ApiController]
    [Route("api/audit-log")]
    [Authorize(Roles = "SuperAdmin")]
    public class AuditLogController : ControllerBase
    {
        private readonly AuditService _auditService;

        public AuditLogController(AuditService auditService)
        {
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>Retrieves the most recent audit entries, newest first.</summary>
        [HttpGet]
        public IActionResult GetRecent()
        {
            try
            {
                var entries = _auditService.GetRecent();
                return Ok(new { items = entries.Select(AuditLogResponseDto.FromAuditLog), count = entries.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve audit log", details = ex.Message });
            }
        }
    }
}
