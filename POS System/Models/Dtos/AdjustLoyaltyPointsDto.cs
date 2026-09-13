namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Request body for POST api/customers/{phone}/loyalty-adjustment.
    /// </summary>
    public class AdjustLoyaltyPointsDto
    {
        /// <summary>Positive to credit, negative to debit. Never zero.</summary>
        public required int Points { get; set; }

        /// <summary>Optional note explaining the manual adjustment (e.g. "Goodwill for late order").</summary>
        public string? Reason { get; set; }
    }
}
