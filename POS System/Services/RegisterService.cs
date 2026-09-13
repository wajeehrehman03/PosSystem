using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    /// <summary>
    /// Register/terminal management (SuperAdmin) and lookup (used by ShiftService.OpenShift).
    /// </summary>
    public class RegisterService
    {
        private readonly AppDbContext _context;

        public RegisterService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Register Create(string code, string? name)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Code is required");

            var normalizedCode = code.Trim();
            if (GetByCode(normalizedCode) != null)
                throw new InvalidOperationException($"A register with code '{normalizedCode}' already exists");

            var register = new Register
            {
                Code = normalizedCode,
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                IsActive = true
            };

            _context.Registers.Add(register);
            _context.SaveChanges();
            return register;
        }

        public List<Register> GetAll()
        {
            return _context.Registers.ToList();
        }

        public List<Register> GetAllActive()
        {
            return _context.Registers.Where(r => r.IsActive).ToList();
        }

        /// <summary>Case-insensitive lookup, matching how Product.Sku/DiscountCode.Code are looked up.</summary>
        public Register? GetByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var normalizedCode = code.Trim().ToLower();
            return _context.Registers.FirstOrDefault(r => r.Code.ToLower() == normalizedCode);
        }

        public Register Deactivate(Register register)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));

            if (!register.IsActive)
                throw new InvalidOperationException($"Register '{register.Code}' is already inactive");

            register.IsActive = false;
            _context.Registers.Update(register);
            _context.SaveChanges();
            return register;
        }

        /// <summary>Reactivates a previously deactivated register so it can be picked again when opening a shift.</summary>
        public Register Activate(Register register)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));

            if (register.IsActive)
                throw new InvalidOperationException($"Register '{register.Code}' is already active");

            register.IsActive = true;
            _context.Registers.Update(register);
            _context.SaveChanges();
            return register;
        }
    }
}
