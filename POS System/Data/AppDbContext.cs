using Microsoft.EntityFrameworkCore;
using PosWebApi.Models;

namespace PosWebApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // 1. User & Authentication Tables
        public DbSet<User> Users { get; set; } = null!;

        // 2. Product & Inventory Tables
        public DbSet<Product> Products { get; set; } = null!;

        // 3. Order & Checkout Tables
        public DbSet<Order> Orders { get; set; } = null!;

        // 4. Cart Tables
        public DbSet<CartItem> CartItems { get; set; } = null!;

        // 5. Shift & Register Reconciliation Tables
        public DbSet<Shift> Shifts { get; set; } = null!;
        public DbSet<CashMovement> CashMovements { get; set; } = null!;

        // 6. Payment Tables
        public DbSet<Payment> Payments { get; set; } = null!;

        // 7. Order Line Item Tables
        public DbSet<OrderItem> OrderItems { get; set; } = null!;

        // 8. Promotions & Loyalty Tables
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<DiscountCode> DiscountCodes { get; set; } = null!;
        public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; } = null!;

        // 9. Register Tables
        public DbSet<Register> Registers { get; set; } = null!;

        // 10. Refund Tables
        public DbSet<Refund> Refunds { get; set; } = null!;
        public DbSet<RefundItem> RefundItems { get; set; } = null!;

        // 11. Audit Tables
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                .Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.PasswordHash)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(UserRoleConstants.Cashier);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Role);

            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            modelBuilder.Entity<User>()
                .Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure Product entity
            modelBuilder.Entity<Product>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Product>()
                .Property(p => p.Sku)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Sku)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Product>()
                .Property(p => p.StockQuantity)
                .IsRequired()
                .HasDefaultValue(0);

            modelBuilder.Entity<Product>()
                .Property(p => p.MinimumStockThreshold)
                .IsRequired()
                .HasDefaultValue(5);

            // CatalogManager.GetLowStockProducts compares these two columns to each other
            // (StockQuantity <= MinimumStockThreshold) rather than against a literal, so this
            // composite index mainly enables an index-only scan (avoiding a full row/heap lookup
            // per row) rather than a true range-scan seek - still a real win as the table grows,
            // just a more modest one than a literal-comparison index would give.
            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.StockQuantity, p.MinimumStockThreshold });

            // Configure CartItem entity
            modelBuilder.Entity<CartItem>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<CartItem>()
                .Property(c => c.UserId)
                .IsRequired();

            modelBuilder.Entity<CartItem>()
                .Property(c => c.ProductId)
                .IsRequired();

            modelBuilder.Entity<CartItem>()
                .Property(c => c.Quantity)
                .IsRequired()
                .HasDefaultValue(1);

            modelBuilder.Entity<CartItem>()
                .Property(c => c.IsCheckedOut)
                .IsRequired()
                .HasDefaultValue(false);

            modelBuilder.Entity<CartItem>()
                .HasIndex(c => new { c.UserId, c.IsCheckedOut });

            modelBuilder.Entity<CartItem>()
                .HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Order entity
            modelBuilder.Entity<Order>()
                .HasKey(o => o.Id);

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderNumber)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(o => o.Subtotal)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(o => o.TaxAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(o => o.ChangeDue)
                .HasPrecision(18, 2)
                .IsRequired()
                .HasDefaultValue(0m);

            modelBuilder.Entity<Order>()
                .Property(o => o.OrderDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Speeds up date-range reporting queries (e.g. a future "today's sales" / X-Z-Report
            // style aggregate) that would otherwise scan the whole table as it grows.
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderDate);

            modelBuilder.Entity<Order>()
                .Property(o => o.ProcessedByCashier)
                .HasMaxLength(100);

            modelBuilder.Entity<Order>()
                .Property(o => o.ShiftId)
                .IsRequired(false);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Shift)
                .WithMany()
                .HasForeignKey(o => o.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ShiftId);

            modelBuilder.Entity<Order>()
                .Property(o => o.CustomerId)
                .IsRequired(false);

            // Restrict: no customer-delete endpoint exists, but if one is ever added, historical
            // order/loyalty data must not disappear with it - same rationale as Shift.Cashier.
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerId);

            modelBuilder.Entity<Order>()
                .Property(o => o.DiscountCodeId)
                .IsRequired(false);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.DiscountCode)
                .WithMany()
                .HasForeignKey(o => o.DiscountCodeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.DiscountCodeId);

            modelBuilder.Entity<Order>()
                .Property(o => o.DiscountAmount)
                .HasPrecision(18, 2)
                .IsRequired()
                .HasDefaultValue(0m);

            modelBuilder.Entity<Order>()
                .Property(o => o.PointsEarned)
                .IsRequired()
                .HasDefaultValue(0);

            modelBuilder.Entity<Order>()
                .Property(o => o.PointsRedeemed)
                .IsRequired()
                .HasDefaultValue(0);

            modelBuilder.Entity<Order>()
                .Property(o => o.ClientTransactionId)
                .HasMaxLength(100);

            // Nullable + unique: MySQL treats each NULL as distinct in a unique index, so any
            // number of ordinary online orders (ClientTransactionId == null) coexist fine, while
            // a retried offline sync with the same non-null key is rejected as a duplicate -
            // exactly the idempotency guarantee this column exists for.
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ClientTransactionId)
                .IsUnique();

            // Configure Shift entity
            modelBuilder.Entity<Shift>()
                .HasKey(s => s.Id);

            // Configure Register entity
            modelBuilder.Entity<Register>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<Register>()
                .Property(r => r.Code)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<Register>()
                .HasIndex(r => r.Code)
                .IsUnique();

            modelBuilder.Entity<Register>()
                .Property(r => r.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<Register>()
                .Property(r => r.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            modelBuilder.Entity<Register>()
                .Property(r => r.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Restrict: a register with shift history must survive being deactivated/removed
            // from future use, same rationale as Shift.Cashier.
            modelBuilder.Entity<Shift>()
                .HasOne(s => s.Register)
                .WithMany()
                .HasForeignKey(s => s.RegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Shift>()
                .HasIndex(s => s.RegisterId);

            modelBuilder.Entity<Shift>()
                .Property(s => s.OpeningFloat)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Shift>()
                .Property(s => s.ClosingCountedAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Shift>()
                .Property(s => s.OpenedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            modelBuilder.Entity<Shift>()
                .Property(s => s.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue(ShiftStatusConstants.Open);

            // A cashier can only have one Open shift at a time (enforced in ShiftService, not
            // a DB constraint, since "one Open row per cashier" isn't expressible as a simple
            // unique index without a filtered/partial index). This index just makes that
            // lookup (and the CashierId+Status query ShiftService/PosEngine both run) fast.
            modelBuilder.Entity<Shift>()
                .HasIndex(s => new { s.CashierId, s.Status });

            // Financial audit data - don't cascade-delete a cashier's shift history if the
            // User row is ever removed (unlike CartItem's Cascade, which is fine for ephemeral
            // cart data). No user-delete endpoint exists today, so this is precautionary.
            modelBuilder.Entity<Shift>()
                .HasOne(s => s.Cashier)
                .WithMany()
                .HasForeignKey(s => s.CashierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure CashMovement entity
            modelBuilder.Entity<CashMovement>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<CashMovement>()
                .Property(c => c.Type)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<CashMovement>()
                .Property(c => c.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<CashMovement>()
                .Property(c => c.Note)
                .HasMaxLength(255);

            modelBuilder.Entity<CashMovement>()
                .Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            modelBuilder.Entity<CashMovement>()
                .HasOne(c => c.Shift)
                .WithMany()
                .HasForeignKey(c => c.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CashMovement>()
                .HasIndex(c => c.ShiftId);

            // Configure Payment entity
            modelBuilder.Entity<Payment>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Method)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Payment>()
                .Property(p => p.ReferenceNumber)
                .HasMaxLength(100);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId);

            // Configure OrderItem entity. A real join entity (not a bare many-to-many) so each
            // line item can snapshot the product's name/price as they were at sale time.
            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.Id);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.ProductNameSnapshot)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Quantity)
                .IsRequired();

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPriceSnapshot)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.LineTotal)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not Cascade): a product can still be deleted from the catalog without
            // destroying historical order/receipt data, since the name/price are already
            // snapshotted here rather than live-referenced.
            modelBuilder.Entity<OrderItem>()
                .HasOne<Product>()
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderItem>()
                .HasIndex(oi => oi.OrderId);

            // Configure Customer entity
            modelBuilder.Entity<Customer>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Customer>()
                .Property(c => c.Phone)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Phone)
                .IsUnique();

            modelBuilder.Entity<Customer>()
                .Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<Customer>()
                .Property(c => c.LoyaltyPoints)
                .IsRequired()
                .HasDefaultValue(0);

            modelBuilder.Entity<Customer>()
                .Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Configure DiscountCode entity
            modelBuilder.Entity<DiscountCode>()
                .HasKey(d => d.Id);

            modelBuilder.Entity<DiscountCode>()
                .Property(d => d.Code)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<DiscountCode>()
                .HasIndex(d => d.Code)
                .IsUnique();

            modelBuilder.Entity<DiscountCode>()
                .Property(d => d.Type)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<DiscountCode>()
                .Property(d => d.Value)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<DiscountCode>()
                .Property(d => d.MinSubtotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<DiscountCode>()
                .Property(d => d.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Configure LoyaltyTransaction entity
            modelBuilder.Entity<LoyaltyTransaction>()
                .HasKey(l => l.Id);

            modelBuilder.Entity<LoyaltyTransaction>()
                .Property(l => l.Type)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<LoyaltyTransaction>()
                .Property(l => l.Points)
                .IsRequired();

            modelBuilder.Entity<LoyaltyTransaction>()
                .Property(l => l.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Restrict: an audit ledger row must survive its customer/order being removed via
            // any future delete endpoint, same rationale as Shift.Cashier and OrderItem.Product.
            modelBuilder.Entity<LoyaltyTransaction>()
                .HasOne(l => l.Customer)
                .WithMany()
                .HasForeignKey(l => l.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoyaltyTransaction>()
                .HasOne(l => l.Order)
                .WithMany()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LoyaltyTransaction>()
                .HasIndex(l => l.CustomerId);

            // Configure Refund entity
            modelBuilder.Entity<Refund>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<Refund>()
                .Property(r => r.Method)
                .IsRequired()
                .HasMaxLength(20);

            modelBuilder.Entity<Refund>()
                .Property(r => r.Reason)
                .HasMaxLength(255);

            modelBuilder.Entity<Refund>()
                .Property(r => r.SubtotalRefunded)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Refund>()
                .Property(r => r.TaxRefunded)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Refund>()
                .Property(r => r.TotalRefunded)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<Refund>()
                .Property(r => r.ProcessedByCashier)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Refund>()
                .Property(r => r.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Restrict: an order's refund history must survive the order itself never being
            // deletable in practice, same defensive rationale used throughout this file.
            modelBuilder.Entity<Refund>()
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Refund>()
                .HasOne(r => r.Shift)
                .WithMany()
                .HasForeignKey(r => r.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Refund>()
                .HasIndex(r => r.OrderId);

            modelBuilder.Entity<Refund>()
                .HasIndex(r => r.ShiftId);

            // Configure RefundItem entity
            modelBuilder.Entity<RefundItem>()
                .HasKey(ri => ri.Id);

            modelBuilder.Entity<RefundItem>()
                .Property(ri => ri.ProductNameSnapshot)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<RefundItem>()
                .Property(ri => ri.UnitPriceSnapshot)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<RefundItem>()
                .Property(ri => ri.LineTotal)
                .HasPrecision(18, 2)
                .IsRequired();

            modelBuilder.Entity<RefundItem>()
                .HasOne(ri => ri.Refund)
                .WithMany(r => r.Items)
                .HasForeignKey(ri => ri.RefundId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: needed to keep summing "already refunded quantity" against the original
            // OrderItem reliable - it must never disappear out from under a RefundItem.
            modelBuilder.Entity<RefundItem>()
                .HasOne(ri => ri.OrderItem)
                .WithMany()
                .HasForeignKey(ri => ri.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefundItem>()
                .HasOne<Product>()
                .WithMany()
                .HasForeignKey(ri => ri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RefundItem>()
                .HasIndex(ri => ri.RefundId);

            modelBuilder.Entity<RefundItem>()
                .HasIndex(ri => ri.OrderItemId);

            // Configure AuditLog entity
            modelBuilder.Entity<AuditLog>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Username)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Details)
                .HasMaxLength(500);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.CreatedAt);

            // Seed default accounts
            SeedDefaultUsers(modelBuilder);
        }

        /// <summary>
        /// Seeds default SuperAdmin and Cashier accounts for initial system setup.
        /// These are created only if the Users table is empty.
        /// Default credentials:
        /// - SuperAdmin: admin / Admin@123456
        /// - Cashier: cashier / Cashier@123456
        /// The hash and timestamp below are fixed literals (not computed at model-build time):
        /// HasData is diffed by the migration generator on every build, so a value that changes
        /// between builds (a fresh BCrypt salt, DateTime.UtcNow) produces a spurious insert/delete
        /// pair in every subsequent migration.
        /// </summary>
        private static readonly DateTime SeedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static void SeedDefaultUsers(ModelBuilder modelBuilder)
        {
            const int superAdminId = 1;
            const int cashierId = 2;

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = superAdminId,
                    Username = "admin",
                    Email = "admin@possystem.local",
                    PasswordHash = "$2a$11$.tIx7BZ9jxcDSMwpxwS36up/C7fMHTXG3AbOTgyqo.H9uLqYi8ci.",
                    Role = UserRoleConstants.SuperAdmin,
                    CreatedAt = SeedTimestamp,
                    IsActive = true,
                    LastLoginAt = null
                },
                new User
                {
                    Id = cashierId,
                    Username = "cashier",
                    Email = "cashier@possystem.local",
                    PasswordHash = "$2a$11$tpJJadQTA.kaP0FgNitXyuNxPGy6X8lLdRr46/nHJqza6SKevrQvS",
                    Role = UserRoleConstants.Cashier,
                    CreatedAt = SeedTimestamp,
                    IsActive = true,
                    LastLoginAt = null
                }
            );
        }
    }
}
