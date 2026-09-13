namespace PosWebApi.Models
{
    /// <summary>
    /// A promotional code that can be applied at checkout for a percent or fixed-amount discount.
    /// </summary>
    public class DiscountCode
    {
        public int Id { get; set; }

        /// <summary>Looked up case-insensitively (see DiscountCodeService).</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>One of DiscountCodeTypeConstants.</summary>
        public string Type { get; set; } = DiscountCodeTypeConstants.Percent;

        /// <summary>Percent (e.g. 10 means 10%) or a fixed dollar amount, depending on Type.</summary>
        public decimal Value { get; set; }

        /// <summary>Minimum cart subtotal required to apply this code, if any.</summary>
        public decimal? MinSubtotal { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Helper class for discount code type constants and validation, mirroring UserRoleConstants.
    /// </summary>
    public static class DiscountCodeTypeConstants
    {
        public const string Percent = "Percent";
        public const string FixedAmount = "FixedAmount";

        public static readonly string[] AllTypes = { Percent, FixedAmount };

        public static bool IsValidType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            return AllTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Returns the canonically-cased type name for a valid (case-insensitive) match.</summary>
        public static string Normalize(string type)
        {
            return AllTypes.First(t => t.Equals(type, StringComparison.OrdinalIgnoreCase));
        }
    }
}
