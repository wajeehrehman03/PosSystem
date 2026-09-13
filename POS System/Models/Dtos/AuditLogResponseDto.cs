namespace PosWebApi.Models.Dtos
{
    public class AuditLogResponseDto
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public required string Username { get; set; }
        public required string Action { get; set; }
        public required string EntityType { get; set; }
        public int? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }

        public static AuditLogResponseDto FromAuditLog(AuditLog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            return new AuditLogResponseDto
            {
                Id = log.Id,
                UserId = log.UserId,
                Username = log.Username,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Details = log.Details,
                CreatedAt = log.CreatedAt
            };
        }
    }
}
