namespace PosWebApi.Models.Dtos
{
    /// <summary>The outcome of replaying a single QueuedSaleDto - see SyncSaleStatusConstants.</summary>
    public class SyncSaleResultDto
    {
        public string ClientTransactionId { get; set; } = string.Empty;

        /// <summary>One of SyncSaleStatusConstants.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Set when Status is Created or AlreadyProcessed.</summary>
        public int? OrderId { get; set; }

        /// <summary>Set when Status is Failed - detailed enough to act on (which product, requested vs. available quantity, etc.).</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Helper class for sync-result status constants, mirroring UserRoleConstants.
    /// </summary>
    public static class SyncSaleStatusConstants
    {
        /// <summary>A new order was created from this queued sale.</summary>
        public const string Created = "Created";

        /// <summary>An order with this ClientTransactionId already existed - not reprocessed (idempotent retry).</summary>
        public const string AlreadyProcessed = "AlreadyProcessed";

        /// <summary>This entry failed (e.g. insufficient stock, unknown product, no open shift, invalid tenders) - see ErrorMessage. Other entries in the same batch are unaffected.</summary>
        public const string Failed = "Failed";
    }
}
