namespace PosWebApi.Models
{
    /// <summary>
    /// A single tender applied toward an Order. An Order can have multiple Payments
    /// (split-tender sales, e.g. part cash, part card).
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        /// <summary>One of PaymentMethodConstants.</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// The amount actually tendered by this method. For Cash, this is the raw amount
        /// handed over (which may exceed what was owed - see Order.ChangeDue for the portion
        /// returned to the customer). Card/gift card tenders can never exceed what they're
        /// applying toward the total (enforced in PosEngine.Checkout).
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional external reference (card terminal auth code, gift card number). Not used
        /// for Cash.
        /// </summary>
        public string? ReferenceNumber { get; set; }
    }

    /// <summary>
    /// Helper class for payment method constants and validation, mirroring UserRoleConstants.
    /// </summary>
    public static class PaymentMethodConstants
    {
        public const string Cash = "Cash";
        public const string Card = "Card";
        public const string GiftCard = "GiftCard";

        public static readonly string[] AllMethods = { Cash, Card, GiftCard };

        public static bool IsValidMethod(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
                return false;

            return AllMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
        }
    }
}
