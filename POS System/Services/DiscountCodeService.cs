using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    /// <summary>
    /// Discount code management (SuperAdmin) and lookup (used by PosEngine.Checkout).
    /// </summary>
    public class DiscountCodeService
    {
        private readonly AppDbContext _context;

        public DiscountCodeService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public DiscountCode Create(string code, string type, decimal value, decimal? minSubtotal, DateTime? expiresAt)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required");

            if (!DiscountCodeTypeConstants.IsValidType(type))
                throw new ArgumentException($"Invalid discount type '{type}'. Valid types are: {string.Join(", ", DiscountCodeTypeConstants.AllTypes)}");

            if (value <= 0)
                throw new ArgumentException("Value must be greater than 0");

            var normalizedType = DiscountCodeTypeConstants.Normalize(type);
            if (normalizedType == DiscountCodeTypeConstants.Percent && value > 100)
                throw new ArgumentException("A Percent discount cannot exceed 100");

            if (minSubtotal.HasValue && minSubtotal.Value < 0)
                throw new ArgumentException("MinSubtotal cannot be negative");

            var normalizedCode = code.Trim();
            if (GetByCode(normalizedCode) != null)
                throw new InvalidOperationException($"A discount code '{normalizedCode}' already exists");

            var discountCode = new DiscountCode
            {
                Code = normalizedCode,
                Type = normalizedType,
                Value = value,
                MinSubtotal = minSubtotal,
                ExpiresAt = expiresAt,
                IsActive = true
            };

            _context.DiscountCodes.Add(discountCode);
            _context.SaveChanges();
            return discountCode;
        }

        public List<DiscountCode> GetAll()
        {
            return _context.DiscountCodes.ToList();
        }

        /// <summary>Case-insensitive lookup, matching how Product.Sku is looked up in CatalogManager.</summary>
        public DiscountCode? GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalizedCode = code.Trim().ToLower();
            return _context.DiscountCodes.FirstOrDefault(d => d.Code.ToLower() == normalizedCode);
        }

        public DiscountCode Deactivate(DiscountCode discountCode)
        {
            if (discountCode == null)
                throw new ArgumentNullException(nameof(discountCode));

            if (!discountCode.IsActive)
                throw new InvalidOperationException($"Discount code '{discountCode.Code}' is already inactive");

            discountCode.IsActive = false;
            _context.DiscountCodes.Update(discountCode);
            _context.SaveChanges();
            return discountCode;
        }
    }
}
