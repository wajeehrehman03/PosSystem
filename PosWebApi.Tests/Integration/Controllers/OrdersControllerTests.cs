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
    /// Integration tests for OrdersController: order lookup and receipt generation (PDF/ESC-POS).
    /// </summary>
    public class OrdersControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public OrdersControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            _clientUnauthenticated = _factory.GetTestClient();

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

            var openShiftResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "TEST-REGISTER", OpeningFloat = 100.00m });
            openShiftResponse.EnsureSuccessStatusCode();
        }

        public async Task DisposeAsync()
        {
            _clientCashier?.Dispose();
            _clientUnauthenticated?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>Adds a product to the cart and checks out with an exact cash tender, returning the created order's id.</summary>
        private async Task<int> CreateCompletedOrderAsync()
        {
            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", new SaleRequestDto { ProductId = 1, Quantity = 2 });

            var checkoutRequest = new CheckoutRequestDto
            {
                CashierName = "Jane",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 23.00m } }
            };
            var response = await _clientCashier.PostAsJsonAsync("api/pos/checkout", checkoutRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsAsync<dynamic>();
            return (int)content!.order.id;
        }

        #region GetById Tests

        [Fact]
        public async Task GetById_WithExistingOrder_ReturnsOkWithItemsAndPayments()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientCashier.GetAsync($"api/orders/{orderId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var order = (await response.Content.ReadAsAsync<OrderResponseDto>())!;
            order.Id.Should().Be(orderId);
            order.Items.Should().ContainSingle(i => i.ProductId == 1 && i.Quantity == 2);
            order.Payments.Should().ContainSingle(p => p.Method == PaymentMethodConstants.Cash);
        }

        [Fact]
        public async Task GetById_WithUnknownId_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/orders/999999");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetById_WithoutAuthentication_ReturnsUnauthorized()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientUnauthenticated.GetAsync($"api/orders/{orderId}");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetById_ForOrderWithLoyaltyCustomer_ReflectsCustomerAndPointsEarned()
        {
            // Arrange - $10.00 subtotal (Product 1) -> $11.50 total -> floor(11.50) = 11 points
            var registerResponse = await _clientCashier.PostAsJsonAsync("api/customers", new RegisterCustomerDto { Phone = "5554443333", Name = "Order Lookup Customer" });
            registerResponse.EnsureSuccessStatusCode();
            var customer = (await registerResponse.Content.ReadAsAsync<CustomerResponseDto>())!;

            await _clientCashier.PostAsJsonAsync("api/pos/cart/add", new SaleRequestDto { ProductId = 1, Quantity = 1 });
            var checkoutResponse = await _clientCashier.PostAsJsonAsync("api/pos/checkout", new CheckoutRequestDto
            {
                CashierName = "Jane",
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = 20.00m } },
                CustomerPhone = "5554443333"
            });
            checkoutResponse.EnsureSuccessStatusCode();
            var checkoutContent = await checkoutResponse.Content.ReadAsAsync<dynamic>();
            var orderId = (int)checkoutContent!.order.id;

            // Act
            var response = await _clientCashier.GetAsync($"api/orders/{orderId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var order = (await response.Content.ReadAsAsync<OrderResponseDto>())!;
            order.CustomerId.Should().Be(customer.Id);
            order.PointsEarned.Should().Be(11);
            order.PointsRedeemed.Should().Be(0);
            order.DiscountAmount.Should().Be(0m);
        }

        #endregion

        #region Receipt Tests

        [Fact]
        public async Task GetReceipt_DefaultFormat_ReturnsPdf()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientCashier.GetAsync($"api/orders/{orderId}/receipt");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Should().NotBeEmpty();
            System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
        }

        [Fact]
        public async Task GetReceipt_PdfFormat_ReturnsPdf()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientCashier.GetAsync($"api/orders/{orderId}/receipt?format=pdf");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        }

        [Fact]
        public async Task GetReceipt_EscPosFormat_ReturnsOctetStream()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientCashier.GetAsync($"api/orders/{orderId}/receipt?format=escpos");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            bytes.Should().NotBeEmpty();
            bytes.Take(2).Should().Equal(new byte[] { 0x1B, 0x40 }); // ESC @ init sequence
        }

        [Fact]
        public async Task GetReceipt_WithUnknownFormat_ReturnsBadRequest()
        {
            var orderId = await CreateCompletedOrderAsync();

            var response = await _clientCashier.GetAsync($"api/orders/{orderId}/receipt?format=xml");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetReceipt_WithUnknownOrderId_ReturnsNotFound()
        {
            var response = await _clientCashier.GetAsync("api/orders/999999/receipt");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion
    }
}
