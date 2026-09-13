using PosWebApi.Models;
using PosWebApi.Models.Dtos;

namespace PosWebApi.Tests.Fixtures
{
    /// <summary>
    /// Builder class for creating test data entities and DTOs with fluent interface.
    /// </summary>
    public class TestDataBuilder
    {
        /// <summary>
        /// Creates a builder for User entities.
        /// </summary>
        public static UserBuilder CreateUser()
        {
            return new UserBuilder();
        }

        /// <summary>
        /// Creates a builder for Product entities.
        /// </summary>
        public static ProductBuilder CreateProduct()
        {
            return new ProductBuilder();
        }

        /// <summary>
        /// Creates a builder for RegisterDto.
        /// </summary>
        public static RegisterDtoBuilder CreateRegisterDto()
        {
            return new RegisterDtoBuilder();
        }

        /// <summary>
        /// Creates a builder for LoginDto.
        /// </summary>
        public static LoginDtoBuilder CreateLoginDto()
        {
            return new LoginDtoBuilder();
        }

        /// <summary>
        /// Creates a builder for CartItem entities.
        /// </summary>
        public static CartItemBuilder CreateCartItem()
        {
            return new CartItemBuilder();
        }

        /// <summary>
        /// Creates a builder for Order entities.
        /// </summary>
        public static OrderBuilder CreateOrder()
        {
            return new OrderBuilder();
        }

        /// <summary>
        /// Creates a builder for OrderItem entities.
        /// </summary>
        public static OrderItemBuilder CreateOrderItem()
        {
            return new OrderItemBuilder();
        }

        /// <summary>
        /// Creates a builder for Shift entities.
        /// </summary>
        public static ShiftBuilder CreateShift()
        {
            return new ShiftBuilder();
        }

        /// <summary>
        /// Creates a builder for Customer entities.
        /// </summary>
        public static CustomerBuilder CreateCustomer()
        {
            return new CustomerBuilder();
        }

        /// <summary>
        /// Creates a builder for DiscountCode entities.
        /// </summary>
        public static DiscountCodeBuilder CreateDiscountCode()
        {
            return new DiscountCodeBuilder();
        }
    }

    /// <summary>
    /// Fluent builder for User entities.
    /// </summary>
    public class UserBuilder
    {
        // AppDbContext seeds default users with Ids 1 (admin) and 2 (cashier) via HasData,
        // which applies even against the EF Core InMemory provider. Start well above that
        // range so tests that don't call WithId() don't collide with the seeded accounts.
        private int _id = 1000;
        private string _username = "testuser";
        private string _email = "testuser@example.com";
        private string _passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123");
        private string _role = UserRoleConstants.Cashier;
        private DateTime _createdAt = DateTime.UtcNow;
        private bool _isActive = true;
        private DateTime? _lastLoginAt = null;

        public UserBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public UserBuilder WithUsername(string username)
        {
            _username = username;
            return this;
        }

        public UserBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public UserBuilder WithPasswordHash(string passwordHash)
        {
            _passwordHash = passwordHash;
            return this;
        }

        public UserBuilder WithRole(string role)
        {
            _role = role;
            return this;
        }

        public UserBuilder AsSuperAdmin()
        {
            _role = UserRoleConstants.SuperAdmin;
            return this;
        }

        public UserBuilder AsCashier()
        {
            _role = UserRoleConstants.Cashier;
            return this;
        }

        public UserBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public UserBuilder WithIsActive(bool isActive)
        {
            _isActive = isActive;
            return this;
        }

        public UserBuilder WithLastLoginAt(DateTime? lastLoginAt)
        {
            _lastLoginAt = lastLoginAt;
            return this;
        }

        public User Build()
        {
            return new User
            {
                Id = _id,
                Username = _username,
                Email = _email,
                PasswordHash = _passwordHash,
                Role = _role,
                CreatedAt = _createdAt,
                IsActive = _isActive,
                LastLoginAt = _lastLoginAt
            };
        }
    }

    /// <summary>
    /// Fluent builder for Product entities.
    /// </summary>
    public class ProductBuilder
    {
        // 0 is EF Core's "unset" sentinel for an int key, so SaveChanges auto-generates a
        // unique Id unless a test calls WithId() explicitly. Avoids Id collisions when a
        // test adds multiple products without caring about specific Ids.
        private int _id = 0;
        private string _sku = "SKU001";
        private string _name = "Test Product";
        private decimal _price = 10.00m;
        private int _stockQuantity = 100;
        private int _minimumStockThreshold = 5;

        public ProductBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public ProductBuilder WithSku(string sku)
        {
            _sku = sku;
            return this;
        }

        public ProductBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public ProductBuilder WithPrice(decimal price)
        {
            _price = price;
            return this;
        }

        public ProductBuilder WithStockQuantity(int quantity)
        {
            _stockQuantity = quantity;
            return this;
        }

        public ProductBuilder WithMinimumStockThreshold(int threshold)
        {
            _minimumStockThreshold = threshold;
            return this;
        }

        public Product Build()
        {
            return new Product
            {
                Id = _id,
                Sku = _sku,
                Name = _name,
                Price = _price,
                StockQuantity = _stockQuantity,
                MinimumStockThreshold = _minimumStockThreshold
            };
        }
    }

    /// <summary>
    /// Fluent builder for RegisterDto.
    /// </summary>
    public class RegisterDtoBuilder
    {
        private string _username = "newuser";
        private string _email = "newuser@example.com";
        private string _password = "Password123";
        private string _confirmPassword = "Password123";
        private string? _role = null;

        public RegisterDtoBuilder WithUsername(string username)
        {
            _username = username;
            return this;
        }

        public RegisterDtoBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public RegisterDtoBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public RegisterDtoBuilder WithConfirmPassword(string confirmPassword)
        {
            _confirmPassword = confirmPassword;
            return this;
        }

        public RegisterDtoBuilder WithRole(string? role)
        {
            _role = role;
            return this;
        }

        public RegisterDtoBuilder AsSuperAdmin()
        {
            _role = UserRoleConstants.SuperAdmin;
            return this;
        }

        public RegisterDtoBuilder AsCashier()
        {
            _role = UserRoleConstants.Cashier;
            return this;
        }

        public RegisterDto Build()
        {
            return new RegisterDto
            {
                Username = _username,
                Email = _email,
                Password = _password,
                ConfirmPassword = _confirmPassword,
                Role = _role
            };
        }
    }

    /// <summary>
    /// Fluent builder for LoginDto.
    /// </summary>
    public class LoginDtoBuilder
    {
        private string _username = "testuser";
        private string _password = "Password123";

        public LoginDtoBuilder WithUsername(string username)
        {
            _username = username;
            return this;
        }

        public LoginDtoBuilder WithPassword(string password)
        {
            _password = password;
            return this;
        }

        public LoginDto Build()
        {
            return new LoginDto
            {
                Username = _username,
                Password = _password
            };
        }
    }

    /// <summary>
    /// Fluent builder for CartItem entities.
    /// </summary>
    public class CartItemBuilder
    {
        private int _id = 1;
        private int _productId = 1;
        private Product? _product = null;
        private int _quantity = 1;

        public CartItemBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public CartItemBuilder WithProductId(int productId)
        {
            _productId = productId;
            return this;
        }

        public CartItemBuilder WithProduct(Product product)
        {
            _product = product;
            _productId = product.Id;
            return this;
        }

        public CartItemBuilder WithQuantity(int quantity)
        {
            _quantity = quantity;
            return this;
        }

        public CartItem Build()
        {
            return new CartItem
            {
                Id = _id,
                ProductId = _productId,
                Product = _product,
                Quantity = _quantity
            };
        }
    }

    /// <summary>
    /// Fluent builder for OrderItem entities.
    /// </summary>
    public class OrderItemBuilder
    {
        private int _id = 1;
        private int _productId = 1;
        private string _productNameSnapshot = "Test Product";
        private int _quantity = 1;
        private decimal _unitPriceSnapshot = 10.00m;

        public OrderItemBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public OrderItemBuilder WithProductId(int productId)
        {
            _productId = productId;
            return this;
        }

        public OrderItemBuilder WithProduct(Product product)
        {
            _productId = product.Id;
            _productNameSnapshot = product.Name;
            _unitPriceSnapshot = product.Price;
            return this;
        }

        public OrderItemBuilder WithProductNameSnapshot(string name)
        {
            _productNameSnapshot = name;
            return this;
        }

        public OrderItemBuilder WithQuantity(int quantity)
        {
            _quantity = quantity;
            return this;
        }

        public OrderItemBuilder WithUnitPriceSnapshot(decimal unitPrice)
        {
            _unitPriceSnapshot = unitPrice;
            return this;
        }

        public OrderItem Build()
        {
            return new OrderItem
            {
                Id = _id,
                ProductId = _productId,
                ProductNameSnapshot = _productNameSnapshot,
                Quantity = _quantity,
                UnitPriceSnapshot = _unitPriceSnapshot,
                LineTotal = _unitPriceSnapshot * _quantity
            };
        }
    }

    /// <summary>
    /// Fluent builder for Order entities.
    /// </summary>
    public class OrderBuilder
    {
        private int _id = 1;
        private Guid _orderNumber = Guid.NewGuid();
        private List<OrderItem> _items = new();
        private decimal _subtotal = 0m;
        private decimal _taxAmount = 0m;
        private decimal _totalAmount = 0m;
        private DateTime _orderDate = DateTime.UtcNow;
        private string _processedByCashier = "Default Cashier";

        public OrderBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public OrderBuilder WithOrderNumber(Guid orderNumber)
        {
            _orderNumber = orderNumber;
            return this;
        }

        public OrderBuilder WithItems(List<OrderItem> items)
        {
            _items = items;
            return this;
        }

        public OrderBuilder AddItem(OrderItem item)
        {
            _items.Add(item);
            return this;
        }

        public OrderBuilder WithSubtotal(decimal subtotal)
        {
            _subtotal = subtotal;
            return this;
        }

        public OrderBuilder WithTaxAmount(decimal taxAmount)
        {
            _taxAmount = taxAmount;
            return this;
        }

        public OrderBuilder WithTotalAmount(decimal totalAmount)
        {
            _totalAmount = totalAmount;
            return this;
        }

        public OrderBuilder WithOrderDate(DateTime orderDate)
        {
            _orderDate = orderDate;
            return this;
        }

        public OrderBuilder WithProcessedByCashier(string cashierName)
        {
            _processedByCashier = cashierName;
            return this;
        }

        public Order Build()
        {
            return new Order
            {
                Id = _id,
                OrderNumber = _orderNumber,
                Items = _items,
                Subtotal = _subtotal,
                TaxAmount = _taxAmount,
                TotalAmount = _totalAmount,
                OrderDate = _orderDate,
                ProcessedByCashier = _processedByCashier
            };
        }
    }

    /// <summary>
    /// Fluent builder for Shift entities.
    /// </summary>
    public class ShiftBuilder
    {
        // 0 is EF Core's "unset" sentinel for an int key, matching ProductBuilder's convention.
        private int _id = 0;
        private int _cashierId = 1000;
        private int _registerId = 1;
        private decimal _openingFloat = 100.00m;
        private DateTime _openedAt = DateTime.UtcNow;
        private DateTime? _closedAt = null;
        private decimal? _closingCountedAmount = null;
        private string _status = ShiftStatusConstants.Open;

        public ShiftBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public ShiftBuilder WithCashierId(int cashierId)
        {
            _cashierId = cashierId;
            return this;
        }

        public ShiftBuilder WithRegisterId(int registerId)
        {
            _registerId = registerId;
            return this;
        }

        public ShiftBuilder WithOpeningFloat(decimal openingFloat)
        {
            _openingFloat = openingFloat;
            return this;
        }

        public ShiftBuilder WithOpenedAt(DateTime openedAt)
        {
            _openedAt = openedAt;
            return this;
        }

        public ShiftBuilder AsClosed(DateTime closedAt, decimal closingCountedAmount)
        {
            _status = ShiftStatusConstants.Closed;
            _closedAt = closedAt;
            _closingCountedAmount = closingCountedAmount;
            return this;
        }

        public Shift Build()
        {
            return new Shift
            {
                Id = _id,
                CashierId = _cashierId,
                RegisterId = _registerId,
                OpeningFloat = _openingFloat,
                OpenedAt = _openedAt,
                ClosedAt = _closedAt,
                ClosingCountedAmount = _closingCountedAmount,
                Status = _status
            };
        }
    }

    /// <summary>
    /// Fluent builder for Customer entities.
    /// </summary>
    public class CustomerBuilder
    {
        // 0 is EF Core's "unset" sentinel for an int key, matching ProductBuilder's convention.
        private int _id = 0;
        private string _phone = "5551234567";
        private string _name = "Test Customer";
        private int _loyaltyPoints = 0;
        private DateTime _createdAt = DateTime.UtcNow;

        public CustomerBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public CustomerBuilder WithPhone(string phone)
        {
            _phone = phone;
            return this;
        }

        public CustomerBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CustomerBuilder WithLoyaltyPoints(int loyaltyPoints)
        {
            _loyaltyPoints = loyaltyPoints;
            return this;
        }

        public CustomerBuilder WithCreatedAt(DateTime createdAt)
        {
            _createdAt = createdAt;
            return this;
        }

        public Customer Build()
        {
            return new Customer
            {
                Id = _id,
                Phone = _phone,
                Name = _name,
                LoyaltyPoints = _loyaltyPoints,
                CreatedAt = _createdAt
            };
        }
    }

    /// <summary>
    /// Fluent builder for DiscountCode entities.
    /// </summary>
    public class DiscountCodeBuilder
    {
        // 0 is EF Core's "unset" sentinel for an int key, matching ProductBuilder's convention.
        private int _id = 0;
        private string _code = "TESTCODE";
        private string _type = DiscountCodeTypeConstants.Percent;
        private decimal _value = 10m;
        private decimal? _minSubtotal = null;
        private DateTime? _expiresAt = null;
        private bool _isActive = true;

        public DiscountCodeBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public DiscountCodeBuilder WithCode(string code)
        {
            _code = code;
            return this;
        }

        public DiscountCodeBuilder WithType(string type)
        {
            _type = type;
            return this;
        }

        public DiscountCodeBuilder WithValue(decimal value)
        {
            _value = value;
            return this;
        }

        public DiscountCodeBuilder WithMinSubtotal(decimal? minSubtotal)
        {
            _minSubtotal = minSubtotal;
            return this;
        }

        public DiscountCodeBuilder WithExpiresAt(DateTime? expiresAt)
        {
            _expiresAt = expiresAt;
            return this;
        }

        public DiscountCodeBuilder AsInactive()
        {
            _isActive = false;
            return this;
        }

        public DiscountCode Build()
        {
            return new DiscountCode
            {
                Id = _id,
                Code = _code,
                Type = _type,
                Value = _value,
                MinSubtotal = _minSubtotal,
                ExpiresAt = _expiresAt,
                IsActive = _isActive
            };
        }
    }
}
