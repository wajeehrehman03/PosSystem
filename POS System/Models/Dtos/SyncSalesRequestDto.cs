namespace PosWebApi.Models.Dtos
{
    /// <summary>Request body for POST api/sync/sales - a batch of offline-queued sales to replay.</summary>
    public class SyncSalesRequestDto
    {
        public List<QueuedSaleDto> Sales { get; set; } = new();
    }
}
