using System.ComponentModel.DataAnnotations.Schema;

namespace PosWebApi.Models
{
    /// <summary>
    /// A loyalty program member, looked up by phone number at checkout to apply/earn points.
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }

        /// <summary>Normalized (digits, optional leading '+') phone number - the lookup key.</summary>
        public string Phone { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Current redeemable points balance (increases on Earn, decreases on Redeem).</summary>
        public int LoyaltyPoints { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Derived from the current points balance (not stored, so it can never go stale) using
        /// the static thresholds in CustomerTierConstants. Note this reflects the *current*
        /// balance, not lifetime points earned - redeeming points can drop a customer's tier.
        /// </summary>
        [NotMapped]
        public string Tier => LoyaltyPoints switch
        {
            >= CustomerTierConstants.GoldThreshold => CustomerTierConstants.Gold,
            >= CustomerTierConstants.SilverThreshold => CustomerTierConstants.Silver,
            _ => CustomerTierConstants.Standard
        };
    }

    /// <summary>
    /// Helper class for customer loyalty tier constants, mirroring UserRoleConstants.
    /// </summary>
    public static class CustomerTierConstants
    {
        public const string Standard = "Standard";
        public const string Silver = "Silver";
        public const string Gold = "Gold";

        public const int SilverThreshold = 1000;
        public const int GoldThreshold = 5000;
    }
}
