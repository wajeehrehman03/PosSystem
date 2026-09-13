using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    /// <summary>
    /// Records who did what to which entity, for SuperAdmin accountability review. Resolves the
    /// acting user from the current request's JWT claims itself (mirroring PosEngine/ShiftService's
    /// GetCurrentUserId pattern) so callers only need to describe the action, not thread identity
    /// through every call site.
    /// </summary>
    public class AuditService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Writes one audit entry, saved immediately with its own SaveChanges - deliberately
        /// separate from whatever transaction the caller's business action ran in, so a slow or
        /// locked audit table can never block a sale, and an audit-write failure can never roll
        /// back the real action it's describing.
        /// </summary>
        public void Log(string action, string entityType, int? entityId, string? details)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier);
            var usernameClaim = user?.FindFirst(ClaimTypes.Name);

            int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out var id) ? id : null;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Username = usernameClaim?.Value ?? "Unknown",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
        }

        public List<AuditLog> GetRecent(int limit = 200)
        {
            return _context.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .ToList();
        }
    }
}
