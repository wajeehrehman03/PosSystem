using System.Text;
using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    /// <summary>
    /// Loyalty customer registration/lookup and loyalty ledger history.
    /// </summary>
    public class CustomerService
    {
        private readonly AppDbContext _context;

        public CustomerService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Normalizes a phone number to digits only (with an optional leading '+') so the same
        /// number always resolves to the same lookup key regardless of how it was formatted
        /// (spaces, dashes, parentheses) when entered. Shared by registration and checkout
        /// lookup so the two can never drift apart and produce false "customer not found" misses.
        /// </summary>
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Phone number is required");

            var sb = new StringBuilder();
            foreach (var ch in phone.Trim())
            {
                if (char.IsDigit(ch) || (ch == '+' && sb.Length == 0))
                    sb.Append(ch);
            }

            var normalized = sb.ToString();
            if (normalized.TrimStart('+').Length < 7)
                throw new ArgumentException("Phone number must contain at least 7 digits");

            return normalized;
        }

        public Customer Register(string phone, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required");

            var normalizedPhone = NormalizePhone(phone);

            if (_context.Customers.Any(c => c.Phone == normalizedPhone))
                throw new InvalidOperationException($"A customer with phone '{normalizedPhone}' is already registered");

            var customer = new Customer
            {
                Phone = normalizedPhone,
                Name = name.Trim(),
                LoyaltyPoints = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            _context.SaveChanges();
            return customer;
        }

        /// <summary>Returns null for a not-found or malformed phone rather than throwing - this is a lookup, not a validated write.</summary>
        public Customer? GetByPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            string normalizedPhone;
            try
            {
                normalizedPhone = NormalizePhone(phone);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return _context.Customers.FirstOrDefault(c => c.Phone == normalizedPhone);
        }

        public List<LoyaltyTransaction> GetLoyaltyHistory(int customerId)
        {
            return _context.LoyaltyTransactions
                .Where(t => t.CustomerId == customerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Manually credits or debits a customer's loyalty balance outside of any sale (e.g.
        /// goodwill points for a new signup, or correcting a mistaken grant). Positive delta
        /// credits, negative debits; zero is rejected as a no-op. A debit that would take the
        /// balance below zero is rejected rather than clamped, so the ledger always sums exactly
        /// to the stored balance.
        /// </summary>
        public Customer AdjustLoyaltyPoints(int customerId, int delta, string? reason)
        {
            if (delta == 0)
                throw new ArgumentException("Points delta must be non-zero");

            var customer = _context.Customers.Find(customerId)
                ?? throw new InvalidOperationException($"No customer found with id {customerId}");

            if (delta < 0 && customer.LoyaltyPoints + delta < 0)
                throw new InvalidOperationException($"Adjustment would take the balance below zero. Current balance: {customer.LoyaltyPoints}, requested debit: {-delta}");

            customer.LoyaltyPoints += delta;

            _context.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                CustomerId = customer.Id,
                OrderId = null,
                Type = delta > 0 ? LoyaltyTransactionTypeConstants.AdjustmentCredit : LoyaltyTransactionTypeConstants.AdjustmentDebit,
                Points = Math.Abs(delta),
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
            return customer;
        }
    }
}
