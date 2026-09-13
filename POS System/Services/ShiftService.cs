using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PosWebApi.Data;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;

namespace PosWebApi.Services
{
    /// <summary>
    /// Handles shift lifecycle (open/close), cash movements, and X/Z-Report reconciliation math.
    /// </summary>
    public class ShiftService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly RegisterService _registerService;

        public ShiftService(AppDbContext context, IHttpContextAccessor httpContextAccessor, RegisterService registerService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _registerService = registerService ?? throw new ArgumentNullException(nameof(registerService));
        }

        /// <summary>
        /// Resolves the authenticated user's ID from the current request's JWT claims.
        /// Duplicated from PosEngine.GetCurrentUserId rather than shared: this codebase keeps
        /// this kind of small, single-line claim lookup local to each service rather than
        /// introducing a shared abstraction for it.
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                throw new UnauthorizedAccessException("No authenticated user found for shift operation");

            return userId;
        }

        public Shift? GetOpenShiftForCashier(int cashierId)
        {
            return _context.Shifts.Include(s => s.Register)
                .FirstOrDefault(s => s.CashierId == cashierId && s.Status == ShiftStatusConstants.Open);
        }

        public Shift? GetCurrentOpenShift()
        {
            return GetOpenShiftForCashier(GetCurrentUserId());
        }

        public Shift? GetById(int id)
        {
            if (id <= 0)
                return null;

            return _context.Shifts.Include(s => s.Register).FirstOrDefault(s => s.Id == id);
        }

        /// <summary>
        /// The sales rung up during a shift, newest first - for a simple "what did I sell this
        /// shift" view rather than the cash-drawer reconciliation math in GenerateReport.
        /// </summary>
        public List<Order> GetOrdersForShift(int shiftId)
        {
            return _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Where(o => o.ShiftId == shiftId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
        }

        /// <summary>
        /// Every sale a cashier has ever rung up, across all of their shifts - for a SuperAdmin
        /// view of "what has this cashier sold in total" rather than one shift at a time.
        /// </summary>
        public List<Order> GetOrdersForCashier(int cashierId)
        {
            return _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Include(o => o.Shift)
                .Where(o => o.Shift != null && o.Shift.CashierId == cashierId)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
        }

        public Shift OpenShift(string registerCode, decimal openingFloat)
        {
            if (string.IsNullOrWhiteSpace(registerCode))
                throw new ArgumentException("Register code is required");

            if (openingFloat < 0)
                throw new ArgumentException("Opening float cannot be negative");

            var register = _registerService.GetByCode(registerCode);
            if (register == null)
                throw new ArgumentException($"Register '{registerCode}' not found");

            if (!register.IsActive)
                throw new InvalidOperationException($"Register '{registerCode}' is not active");

            var userId = GetCurrentUserId();

            if (GetOpenShiftForCashier(userId) != null)
                throw new InvalidOperationException("This cashier already has an open shift. Close it before opening a new one.");

            var shift = new Shift
            {
                CashierId = userId,
                RegisterId = register.Id,
                OpeningFloat = openingFloat,
                Status = ShiftStatusConstants.Open,
                OpenedAt = DateTime.UtcNow
            };

            _context.Shifts.Add(shift);
            _context.SaveChanges();

            shift.Register = register;
            return shift;
        }

        public CashMovement RecordCashMovement(Shift shift, string type, decimal amount, string? note)
        {
            if (shift == null)
                throw new ArgumentNullException(nameof(shift));

            if (!shift.IsOpen())
                throw new InvalidOperationException("Cannot record a cash movement against a closed shift");

            if (!CashMovementTypeConstants.IsValidType(type))
                throw new ArgumentException($"Invalid cash movement type '{type}'. Valid types are: {string.Join(", ", CashMovementTypeConstants.AllTypes)}");

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than 0");

            var movement = new CashMovement
            {
                ShiftId = shift.Id,
                Type = type,
                Amount = amount,
                Note = note,
                CreatedAt = DateTime.UtcNow
            };

            _context.CashMovements.Add(movement);
            _context.SaveChanges();
            return movement;
        }

        public Shift CloseShift(Shift shift, decimal closingCountedAmount)
        {
            if (shift == null)
                throw new ArgumentNullException(nameof(shift));

            if (!shift.IsOpen())
                throw new InvalidOperationException("Shift is already closed");

            if (closingCountedAmount < 0)
                throw new ArgumentException("Closing counted amount cannot be negative");

            shift.Status = ShiftStatusConstants.Closed;
            shift.ClosedAt = DateTime.UtcNow;
            shift.ClosingCountedAmount = closingCountedAmount;

            _context.Shifts.Update(shift);
            _context.SaveChanges();
            return shift;
        }

        /// <summary>
        /// Net cash retained in the drawer from sales in this shift: the sum of Cash-method
        /// Payment amounts actually tendered, minus change handed back out (Order.ChangeDue -
        /// change is always drawn from a cash tender, since PosEngine.Checkout never lets a
        /// card/gift card tender exceed the order total). Card and gift card tenders never
        /// touch the physical drawer, so they're excluded entirely. Replaces the old
        /// "100% of Order.TotalAmount is cash" placeholder now that multi-tender payments exist.
        /// </summary>
        private decimal GetCashSalesTotal(int shiftId)
        {
            var cashTendered = _context.Payments
                .Where(p => p.Order!.ShiftId == shiftId && p.Method == PaymentMethodConstants.Cash)
                .Sum(p => (decimal?)p.Amount) ?? 0m;

            var changeGiven = _context.Orders
                .Where(o => o.ShiftId == shiftId)
                .Sum(o => (decimal?)o.ChangeDue) ?? 0m;

            return cashTendered - changeGiven;
        }

        private decimal GetCashMovementTotal(int shiftId, string type)
        {
            return _context.CashMovements
                .Where(c => c.ShiftId == shiftId && c.Type == type)
                .Sum(c => (decimal?)c.Amount) ?? 0m;
        }

        /// <summary>Cash paid back out to customers via refunds processed against this shift.</summary>
        private decimal GetCashRefundsTotal(int shiftId)
        {
            return _context.Refunds
                .Where(r => r.ShiftId == shiftId && r.Method == PaymentMethodConstants.Cash)
                .Sum(r => (decimal?)r.TotalRefunded) ?? 0m;
        }

        /// <summary>
        /// Builds the cash reconciliation report for a shift. Pass isFinal:false for a live,
        /// non-terminal X-Report; isFinal:true (with the shift's ClosingCountedAmount already
        /// set via CloseShift) for the closing Z-Report.
        /// </summary>
        public ShiftReportDto GenerateReport(Shift shift, bool isFinal)
        {
            if (shift == null)
                throw new ArgumentNullException(nameof(shift));

            var cashSalesTotal = GetCashSalesTotal(shift.Id);
            var cashDropsTotal = GetCashMovementTotal(shift.Id, CashMovementTypeConstants.CashDrop);
            var payOutsTotal = GetCashMovementTotal(shift.Id, CashMovementTypeConstants.PayOut);
            var payInsTotal = GetCashMovementTotal(shift.Id, CashMovementTypeConstants.PayIn);
            var cashRefundsTotal = GetCashRefundsTotal(shift.Id);

            var expectedCash = shift.OpeningFloat + cashSalesTotal - cashDropsTotal - payOutsTotal + payInsTotal - cashRefundsTotal;

            decimal? variance = null;
            if (isFinal && shift.ClosingCountedAmount.HasValue)
                variance = shift.ClosingCountedAmount.Value - expectedCash;

            return new ShiftReportDto
            {
                ShiftId = shift.Id,
                RegisterCode = shift.Register?.Code ?? string.Empty,
                OpeningFloat = shift.OpeningFloat,
                CashSalesTotal = cashSalesTotal,
                CashDropsTotal = cashDropsTotal,
                PayInsTotal = payInsTotal,
                PayOutsTotal = payOutsTotal,
                CashRefundsTotal = cashRefundsTotal,
                ExpectedCash = expectedCash,
                IsFinal = isFinal,
                ClosingCountedAmount = isFinal ? shift.ClosingCountedAmount : null,
                Variance = variance,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }
}
