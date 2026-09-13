namespace PosWebApi.Models
{
    /// <summary>
    /// An append-only ledger entry for a customer's loyalty points balance changing. Customer
    /// .LoyaltyPoints is the maintained running balance; this is the audit trail behind it, kept
    /// consistent with it in the same transaction (see PosEngine.Checkout).
    /// </summary>
    public class LoyaltyTransaction
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        /// <summary>
        /// The order this transaction was generated from. Nullable so a future manual
        /// adjustment path (not built yet) has somewhere to point instead - every transaction
        /// created by checkout today always has one.
        /// </summary>
        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        /// <summary>One of LoyaltyTransactionTypeConstants.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Always positive; direction is implied by Type.</summary>
        public int Points { get; set; }

        /// <summary>Why a manual AdjustmentCredit/AdjustmentDebit was made. Null for every
        /// Earn/Redeem/Refund transaction, which are self-explanatory from their linked Order.</summary>
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Helper class for loyalty transaction type constants and validation, mirroring UserRoleConstants.
    /// </summary>
    public static class LoyaltyTransactionTypeConstants
    {
        public const string Earn = "Earn";
        public const string Redeem = "Redeem";

        /// <summary>A proportional reversal of previously-earned points because the sale (or
        /// part of it) that earned them was refunded. See RefundService.ProcessRefund.</summary>
        public const string Refund = "Refund";

        /// <summary>A manual credit made by staff outside of any sale (e.g. goodwill points, a
        /// migrated balance). See CustomerService.AdjustLoyaltyPoints.</summary>
        public const string AdjustmentCredit = "AdjustmentCredit";

        /// <summary>A manual debit made by staff outside of any sale (e.g. correcting an
        /// over-grant). See CustomerService.AdjustLoyaltyPoints.</summary>
        public const string AdjustmentDebit = "AdjustmentDebit";

        public static readonly string[] AllTypes = { Earn, Redeem, Refund, AdjustmentCredit, AdjustmentDebit };

        public static bool IsValidType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            return AllTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
        }
    }
}
