namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Response body for POST api/sync/sales. Always 200 - per-entry failures are reported here,
    /// not as an HTTP-level error, since one bad entry must never fail the rest of the batch.
    /// </summary>
    public class SyncSalesResponseDto
    {
        public List<SyncSaleResultDto> Results { get; set; } = new();
    }
}
