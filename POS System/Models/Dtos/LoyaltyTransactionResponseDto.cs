namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for a single loyalty ledger entry in responses.
    /// </summary>
    public class LoyaltyTransactionResponseDto
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public required string Type { get; set; }
        public int Points { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        public static LoyaltyTransactionResponseDto FromTransaction(LoyaltyTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            return new LoyaltyTransactionResponseDto
            {
                Id = transaction.Id,
                OrderId = transaction.OrderId,
                Type = transaction.Type,
                Points = transaction.Points,
                Reason = transaction.Reason,
                CreatedAt = transaction.CreatedAt
            };
        }
    }
}
