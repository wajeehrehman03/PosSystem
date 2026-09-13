using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using PosWebApi.Models;
using PosWebApi.Models.Dtos;
using PosWebApi.Tests.Fixtures;

namespace PosWebApi.Tests.Integration.Controllers
{
    /// <summary>
    /// Integration tests for PosController cart, checkout, and administrative operations with RBAC.
    /// </summary>
    public class PosControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientAdmin = null!;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public PosControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            // Unauthenticated client
            _clientUnauthenticated = _factory.GetTestClient();

            // Admin client
            var adminClient = _factory.GetTestClient();
            var adminLoginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("admin")
                .WithPassword("Admin@123456")
                .Build();

            var adminLoginResponse = await adminClient.PostAsJsonAsync("api/auth/login", adminLoginDto);
            adminLoginResponse.EnsureSuccessStatusCode();
            var adminToken = (await adminLoginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            adminClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken.AccessToken);
            _clientAdmin = adminClient;

            // Cashier client
            var cashierClient = _factory.GetTestClient();
            var cashierLoginDto = TestDataBuilder.CreateLoginDto()
                .WithUsername("cashier")
                .WithPassword("Cashier@123456")
                .Build();

            var cashierLoginResponse = await cashierClient.PostAsJsonAsync("api/auth/login", cashierLoginDto);
            cashierLoginResponse.EnsureSuccessStatusCode();
            var cashierToken = (await cashierLoginResponse.Content.ReadAsAsync<TokenResponseDto>())!;
            cashierClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cashierToken.AccessToken);
            _clientCashier = cashierClient;

            // Checkout requires an open shift. Opened here so every test in this class can
            // check out without repeating this arrange step; tests exercising the no-open-shift
            // path close it explicitly first (see Checkout_WithoutOpenShift_ReturnsBadRequest).
            var openShiftResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "TEST-REGISTER", OpeningFloat = 100.00m });
            openShiftResponse.EnsureSuccessStatusCode();
        }

        public async Task DisposeAsync()
        {
            _clientAdmin?.Dispose();
            _clientCashier?.Dispose();
            _clientUnauthenticated?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Builds a single-cash-tender checkout request body. Checkout moved from query-string
        /// params to a JSON body when multi-tender payments were added, since a list of tenders
        /// doesn't belong in a query string.
        /// </summary>
        private static CheckoutRequestDto CashCheckout(string? cashierName, decimal amount)
        {
            return new CheckoutRequestDto
            {
                CashierName = cashierName,
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = amount } }
            };
        }

        #region Authorization Tests

        [Fact]
        public async Task GetCart_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _clientUnauthenticated.GetAsync("api/pos/cart");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetCart_WithCashierRole_ReturnsOk()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/pos/cart");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetCart_WithAdminRole_ReturnsOk()
        {
            // Act
            var response = await _clientAdmin.GetAsync("api/pos/cart");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task AddToCart_WithCashierRole_ReturnsOk()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 2 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task AddToCart_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 2 };

            // Act
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Checkout_WithCashierRole_Succeeds()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Checkout_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/pos/checkout", CashCheckout("Test", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetDailySummary_WithAdminRole_ReturnsOk()
        {
            // Act
            var response = await _clientAdmin.GetAsync("api/pos/daily-summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetDailySummary_WithCashierRole_ReturnsForbidden()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/pos/daily-summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // The old placeholder api/pos/shift/close endpoint (SuperAdmin-only, fake response) was
        // replaced by the real ShiftController (api/shifts/*) - see ShiftControllerTests.cs.

        #endregion

        #region Cart Management Tests

        [Fact]
        public async Task GetCart_WithEmptyCart_ReturnsEmptyList()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/pos/cart");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((int)content!.itemCount).Should().Be(0);
        }

        [Fact]
        public async Task AddToCart_WithValidProduct_ReturnsOk()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 2 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((int)content!.cartItemCount).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AddToCart_WithInvalidProductId_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 999, Quantity = 2 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddToCart_WithNegativeProductId_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = -1, Quantity = 2 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddToCart_WithNegativeQuantity_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = -5 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddToCart_WithZeroQuantity_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 0 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddToCart_WithInsufficientStock_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 4, Quantity = 1 }; // Product 4 has 0 stock

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddToCart_MultipleProducts_BuildsCart()
        {
            // Arrange & Act
            var add1 = new SaleRequestDto { ProductId = 1, Quantity = 2 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add1);

            var add2 = new SaleRequestDto { ProductId = 2, Quantity = 3 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add2);

            // Act
            var getResponse = await _clientCashier.GetAsync("api/pos/cart");

            // Assert
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await getResponse.Content.ReadAsAsync<dynamic>();
            ((int)content!.itemCount).Should().Be(2);
        }

        [Fact]
        public async Task Undo_WithItemsInCart_RemovesLastItem()
        {
            // Arrange
            var add1 = new SaleRequestDto { ProductId = 1, Quantity = 2 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add1);

            var add2 = new SaleRequestDto { ProductId = 2, Quantity = 3 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add2);

            // Act
            var undoResponse = await _clientCashier.PostAsync("api/pos/cart/undo", null);

            // Assert
            undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getResponse = await _clientCashier.GetAsync("api/pos/cart");
            var content = await getResponse.Content.ReadAsAsync<dynamic>();
            ((int)content!.itemCount).Should().Be(1);
        }

        [Fact]
        public async Task Undo_WithEmptyCart_ReturnsBadRequest()
        {
            // Act
            var response = await _clientCashier.PostAsync("api/pos/cart/undo", null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ClearCart_RemovesAllItems()
        {
            // Arrange
            var add1 = new SaleRequestDto { ProductId = 1, Quantity = 2 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add1);

            var add2 = new SaleRequestDto { ProductId = 2, Quantity = 3 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add2);

            // Act
            var clearResponse = await _clientCashier.PostAsync("api/pos/cart/clear", null);

            // Assert
            clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getResponse = await _clientCashier.GetAsync("api/pos/cart");
            var content = await getResponse.Content.ReadAsAsync<dynamic>();
            ((int)content!.itemCount).Should().Be(0);
        }

        #endregion

        #region Checkout Tests

        [Fact]
        public async Task Checkout_WithValidCart_CreatesOrder()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 2 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Cashier1", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Checkout_WithEmptyCart_ReturnsBadRequest()
        {
            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Test", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithoutOpenShift_ReturnsBadRequest()
        {
            // Arrange: close the shift InitializeAsync opened for this client
            var currentResponse = await _clientCashier.GetAsync("api/shifts/current");
            var current = (await currentResponse.Content.ReadAsAsync<ShiftResponseDto>())!;
            var closeResponse = await _clientCashier.PostAsJsonAsync($"api/shifts/{current.Id}/close", new CloseShiftDto { ClosingCountedAmount = 100.00m });
            closeResponse.EnsureSuccessStatusCode();

            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Test", 100m));

            // Assert: PosController.Checkout maps InvalidOperationException to BadRequest,
            // same as the empty-cart case above - consistent with that existing convention.
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithOpenShift_StampsOrderShiftIdReflectedInXReport()
        {
            // Arrange
            var currentResponse = await _clientCashier.GetAsync("api/shifts/current");
            var current = (await currentResponse.Content.ReadAsAsync<ShiftResponseDto>())!;
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 }; // $10, tax $1.50, total $11.50

            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var checkoutResponse = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Test", 100m));
            checkoutResponse.EnsureSuccessStatusCode();

            var reportResponse = await _clientCashier.GetAsync($"api/shifts/{current.Id}/x-report");
            var report = (await reportResponse.Content.ReadAsAsync<ShiftReportDto>())!;

            // Assert: the order's total is reflected in the shift's cash sales / expected cash
            report.CashSalesTotal.Should().Be(11.50m);
            report.ExpectedCash.Should().Be(current.OpeningFloat + 11.50m);
        }

        [Fact]
        public async Task Checkout_WithMixedCashAndCardTender_XReportOnlyCountsCashPortion()
        {
            // Arrange - Product 1: $10.00, tax 15% = $1.50, total $11.50, split $10.00 card + $1.50 cash
            var currentResponse = await _clientCashier.GetAsync("api/shifts/current");
            var current = (await currentResponse.Content.ReadAsAsync<ShiftResponseDto>())!;
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            var checkoutRequest = new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto>
                {
                    new PaymentRequestDto { Method = PaymentMethodConstants.Card, Amount = 10.00m, ReferenceNumber = "AUTH999" },
                    new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 1.50m }
                }
            };

            // Act
            var checkoutResponse = await _clientCashier.PostAsJsonAsync("api/pos/checkout", checkoutRequest);
            checkoutResponse.EnsureSuccessStatusCode();

            var reportResponse = await _clientCashier.GetAsync($"api/shifts/{current.Id}/x-report");
            var report = (await reportResponse.Content.ReadAsAsync<ShiftReportDto>())!;

            // Assert: only the $1.50 cash portion counts toward expected cash - the $10.00 card
            // portion never touches the drawer.
            report.CashSalesTotal.Should().Be(1.50m);
            report.ExpectedCash.Should().Be(current.OpeningFloat + 1.50m);
        }

        [Fact]
        public async Task Checkout_CalculatesCorrectTotal()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 2 }; // 2 × $10 = $20
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            // Subtotal: $20, Tax: $3, Total: $23
            ((decimal)content!.order.subtotal).Should().Be(20.00m);
            ((decimal)content!.order.tax).Should().Be(3.00m);
            ((decimal)content!.order.total).Should().Be(23.00m);
        }

        [Fact]
        public async Task Checkout_WithMultipleItems_CalculatesComplexTotal()
        {
            // Arrange
            var add1 = new SaleRequestDto { ProductId = 1, Quantity = 2 }; // $10 × 2 = $20
            var add2 = new SaleRequestDto { ProductId = 2, Quantity = 1 }; // $20 × 1 = $20
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add1);
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add2);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            // Subtotal: $40, Tax: $6, Total: $46
            ((decimal)content!.order.subtotal).Should().Be(40.00m);
            ((decimal)content!.order.tax).Should().Be(6.00m);
            ((decimal)content!.order.total).Should().Be(46.00m);
        }

        [Fact]
        public async Task Checkout_WithNullCashierName_UsesDefault()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout(null, 100m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((string)content!.order.cashier).Should().Be("Default Cashier");
        }

        [Fact]
        public async Task Checkout_ClearsCartAfterCompletion()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 100m));

            // Assert
            var getResponse = await _clientCashier.GetAsync("api/pos/cart");
            var content = await getResponse.Content.ReadAsAsync<dynamic>();
            ((int)content!.itemCount).Should().Be(0);
        }

        [Fact]
        public async Task Checkout_DeductsStockFromProducts()
        {
            // Arrange - Get initial stock
            var catalogResponse = await _clientCashier.GetAsync("api/catalog/1");
            var initialProduct = await catalogResponse.Content.ReadAsAsync<dynamic>();
            var initialStock = (int)initialProduct!.stockQuantity;

            // Add to cart and checkout
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 5 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);
            await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 100m));

            // Act - Check updated stock
            var updatedCatalogResponse = await _clientCashier.GetAsync("api/catalog/1");
            var updatedProduct = await updatedCatalogResponse.Content.ReadAsAsync<dynamic>();
            var updatedStock = (int)updatedProduct!.stockQuantity;

            // Assert
            updatedStock.Should().Be(initialStock - 5);
        }

        [Fact]
        public async Task Checkout_WithInsufficientTender_ReturnsBadRequest()
        {
            // Arrange - Product 1 total is $11.50; tender only $5.00
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Test", 5.00m));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithCardTenderExceedingTotal_ReturnsBadRequest()
        {
            // Arrange - Product 1 total is $11.50; a card tender can't overpay (no change on a card)
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);
            var checkoutRequest = new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Card, Amount = 20.00m } }
            };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", checkoutRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithNoTenders_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);
            var checkoutRequest = new CheckoutRequestDto { CashierName = "Test", Tenders = new List<PaymentRequestDto>() };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", checkoutRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithSplitTender_ResponseIncludesPaymentsAndChangeDue()
        {
            // Arrange - Product 1 total is $11.50: $10.00 card + $5.00 cash (change due $3.50)
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);
            var checkoutRequest = new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto>
                {
                    new PaymentRequestDto { Method = PaymentMethodConstants.Card, Amount = 10.00m, ReferenceNumber = "AUTH777" },
                    new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 5.00m }
                }
            };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", checkoutRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((decimal)content!.order.changeDue).Should().Be(3.50m);

            var payments = ((IEnumerable<dynamic>)content!.order.payments).ToList();
            payments.Should().HaveCount(2);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public async Task Scenario_FullCheckoutFlow()
        {
            // Step 1: Cashier gets empty cart
            var cartResponse = await _clientCashier.GetAsync("api/pos/cart");
            cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 2: Cashier adds items
            var addDto1 = new SaleRequestDto { ProductId = 1, Quantity = 2 };
            var addResponse1 = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto1);
            addResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

            var addDto2 = new SaleRequestDto { ProductId = 2, Quantity = 1 };
            var addResponse2 = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto2);
            addResponse2.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 3: Cashier views cart
            var viewCartResponse = await _clientCashier.GetAsync("api/pos/cart");
            var cartContent = await viewCartResponse.Content.ReadAsAsync<dynamic>();
            ((int)cartContent!.itemCount).Should().Be(2);

            // Step 4: Cashier undoes last action
            var undoResponse = await _clientCashier.PostAsync("api/pos/cart/undo", null);
            undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 5: Verify cart has 1 item
            var verifyCartResponse = await _clientCashier.GetAsync("api/pos/cart");
            var verifyContent = await verifyCartResponse.Content.ReadAsAsync<dynamic>();
            ((int)verifyContent!.itemCount).Should().Be(1);

            // Step 6: Add back the removed item
            var readdDto = new SaleRequestDto { ProductId = 2, Quantity = 1 };
            var readdResponse = await _clientCashier.PostAsJsonAsync("api/pos/cart/add", readdDto);
            readdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 7: Checkout
            var checkoutResponse = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("JohnDoe", 200m));
            checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Step 8: Verify cart is empty after checkout
            var finalCartResponse = await _clientCashier.GetAsync("api/pos/cart");
            var finalCartContent = await finalCartResponse.Content.ReadAsAsync<dynamic>();
            ((int)finalCartContent!.itemCount).Should().Be(0);
        }

        [Fact]
        public async Task Scenario_MultipleCheckoutsInSequence()
        {
            // First transaction
            var add1 = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add1);
            var checkout1 = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Cashier1", 50m));
            checkout1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Second transaction with same cashier
            var add2 = new SaleRequestDto { ProductId = 2, Quantity = 2 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", add2);
            var checkout2 = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("Cashier1", 100m));
            checkout2.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify both transactions completed
            checkout1.StatusCode.Should().Be(HttpStatusCode.OK);
            checkout2.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Scenario_AdminAndCashierWorkflows()
        {
            // Admin views daily summary (should work)
            var adminSummary = await _clientAdmin.GetAsync("api/pos/daily-summary");
            adminSummary.StatusCode.Should().Be(HttpStatusCode.OK);

            // Cashier performs transaction
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);
            var cashierCheckout = await _clientCashier.PostAsJsonAsync("api/pos/checkout", CashCheckout("TestCashier", 50m));
            cashierCheckout.StatusCode.Should().Be(HttpStatusCode.OK);

            // Admin views daily summary after transaction
            var adminSummaryAfter = await _clientAdmin.GetAsync("api/pos/daily-summary");
            adminSummaryAfter.StatusCode.Should().Be(HttpStatusCode.OK);

            // Cashier cannot view daily summary
            var cashierSummary = await _clientCashier.GetAsync("api/pos/daily-summary");
            cashierSummary.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        #endregion

        #region Loyalty & Discount Checkout Tests

        [Fact]
        public async Task Checkout_WithCustomerPhone_EarnsPointsReflectedInResponse()
        {
            // Arrange - $10.00 subtotal (Product 1) -> $11.50 total -> floor(11.50) = 11 points
            var registerResponse = await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "5552223333", Name = "Loyalty Customer" });
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 100m } },
                CustomerPhone = "5552223333"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((int)content!.order.pointsEarned).Should().Be(11);
            ((int)content!.order.customerLoyaltyBalance).Should().Be(11);
        }

        [Fact]
        public async Task Checkout_WithDiscountCode_ReducesTotalReflectedInResponse()
        {
            // Arrange - $10.00 subtotal, 10% off -> $9.00 taxable -> $1.35 tax -> $10.35 total
            var createCodeResponse = await _clientAdmin.PostAsJsonAsync("api/discount-codes", new CreateDiscountCodeDto
            {
                Code = "POSTEN",
                Type = DiscountCodeTypeConstants.Percent,
                Value = 10m
            });
            createCodeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 100m } },
                DiscountCode = "POSTEN"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((decimal)content!.order.discountAmount).Should().Be(1.00m);
            ((decimal)content!.order.total).Should().Be(10.35m);
        }

        [Fact]
        public async Task Checkout_WithUnknownCustomerPhone_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 100m } },
                CustomerPhone = "5559999999"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Checkout_WithUnknownDiscountCode_ReturnsBadRequest()
        {
            // Arrange
            var addDto = new SaleRequestDto { ProductId = 1, Quantity = 1 };
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", addDto);

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", new CheckoutRequestDto
            {
                CashierName = "Test",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 100m } },
                DiscountCode = "NOSUCHCODE"
            });

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion
    }
}
