namespace PosWebApi.Models
{
    /// <summary>
    /// An append-only record of who did what to which entity - product price changes, product
    /// deletions, new user accounts, etc. Read via GET /api/audit-log (SuperAdmin only).
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        /// <summary>Nullable rather than a hard FK - an audit entry must never be lost just
        /// because the acting user's account was later deleted.</summary>
        public int? UserId { get; set; }

        /// <summary>Snapshotted at write time so the log stays readable even if the acting
        /// user's account is later renamed or removed.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Short verb-based label, e.g. "ProductCreated", "ProductPriceChanged", "ProductDeleted", "UserCreated".</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>The type of entity affected, e.g. "Product", "User".</summary>
        public string EntityType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        /// <summary>Human-readable summary of what changed.</summary>
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
