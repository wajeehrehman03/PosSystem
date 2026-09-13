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
    /// Integration tests for SyncController - replaying offline-queued sales, idempotency, and
    /// per-entry failure isolation within a batch.
    /// </summary>
    public class SyncControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientCashier = null!;

        public SyncControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
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

            var openShiftResponse = await _clientCashier.PostAsJsonAsync("api/shifts/open", new OpenShiftDto { RegisterCode = "SYNC-TEST-REGISTER", OpeningFloat = 100.00m });
            openShiftResponse.EnsureSuccessStatusCode();
        }

        public async Task DisposeAsync()
        {
            _clientCashier?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        private static QueuedSaleDto ValidQueuedSale(string clientTransactionId, int productId, int quantity, decimal tenderAmount, DateTime? occurredAt = null)
        {
            return new QueuedSaleDto
            {
                ClientTransactionId = clientTransactionId,
                OccurredAt = occurredAt ?? DateTime.UtcNow.AddHours(-2),
                CashierName = "Offline Cashier",
                Items = new List<QueuedSaleItemDto> { new QueuedSaleItemDto { ProductId = productId, Quantity = quantity } },
                Tenders = new List<PaymentRequestDto> { new PaymentRequestDto { Method = PaymentMethodConstants.Cash, Amount = tenderAmount } }
            };
        }

        [Fact]
        public async Task SyncSales_WithTwoValidQueuedSales_BothSucceedAndDeductStock()
        {
            // Arrange - Product 1: price 10.00, stock 100 -> qty 1 => subtotal 10, tax 1.50, total 11.50
            var occurredAt = new DateTime(2026, 1, 1, 8, 30, 0, DateTimeKind.Utc);
            var sale1 = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 1, tenderAmount: 11.50m, occurredAt);
            var sale2 = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 2, tenderAmount: 23.00m, occurredAt);
            var request = new SyncSalesRequestDto { Sales = new List<QueuedSaleDto> { sale1, sale2 } };

            var stockBefore = await (await _clientCashier.GetAsync("api/catalog/1")).Content.ReadAsAsync<Product>();

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/sync/sales", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadAsAsync<SyncSalesResponseDto>();
            result!.Results.Should().HaveCount(2);
            result.Results.Should().OnlyContain(r => r.Status == SyncSaleStatusConstants.Created && r.OrderId.HasValue);

            var order1Result = result.Results.First(r => r.ClientTransactionId == sale1.ClientTransactionId);
            var orderResponse = await _clientCashier.GetAsync($"api/orders/{order1Result.OrderId}");
            orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var order = await orderResponse.Content.ReadAsAsync<OrderResponseDto>();
            order!.OrderDate.Should().Be(occurredAt);

            var stockAfter = await (await _clientCashier.GetAsync("api/catalog/1")).Content.ReadAsAsync<Product>();
            stockAfter!.StockQuantity.Should().Be(stockBefore!.StockQuantity - 3); // 1 + 2 units sold
        }

        [Fact]
        public async Task SyncSales_ResubmittingSameBatch_ReturnsAlreadyProcessedAndDoesNotDoubleDeduct()
        {
            // Arrange
            var sale = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 1, tenderAmount: 11.50m);
            var request = new SyncSalesRequestDto { Sales = new List<QueuedSaleDto> { sale } };

            var firstResponse = await _clientCashier.PostAsJsonAsync("api/sync/sales", request);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var firstResult = await firstResponse.Content.ReadAsAsync<SyncSalesResponseDto>();
            firstResult!.Results.Single().Status.Should().Be(SyncSaleStatusConstants.Created);
            var firstOrderId = firstResult.Results.Single().OrderId;

            var stockAfterFirst = await (await _clientCashier.GetAsync("api/catalog/1")).Content.ReadAsAsync<Product>();

            // Act - resubmit the exact same batch (same ClientTransactionId)
            var secondResponse = await _clientCashier.PostAsJsonAsync("api/sync/sales", request);

            // Assert
            secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var secondResult = await secondResponse.Content.ReadAsAsync<SyncSalesResponseDto>();
            var entry = secondResult!.Results.Single();
            entry.Status.Should().Be(SyncSaleStatusConstants.AlreadyProcessed);
            entry.OrderId.Should().Be(firstOrderId);

            var stockAfterSecond = await (await _clientCashier.GetAsync("api/catalog/1")).Content.ReadAsAsync<Product>();
            stockAfterSecond!.StockQuantity.Should().Be(stockAfterFirst!.StockQuantity);

            var orderResponse = await _clientCashier.GetAsync($"api/orders/{firstOrderId}");
            var order = await orderResponse.Content.ReadAsAsync<OrderResponseDto>();
            order!.Payments.Should().HaveCount(1); // not duplicated
        }

        [Fact]
        public async Task SyncSales_WithOneEntryInsufficientStock_FailsOnlyThatEntry()
        {
            // Arrange - Product 3 has only 2 in stock; requesting 5 must fail that entry only.
            var goodSale = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 1, tenderAmount: 11.50m);
            var badSale = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 3, quantity: 5, tenderAmount: 200.00m);
            var request = new SyncSalesRequestDto { Sales = new List<QueuedSaleDto> { goodSale, badSale } };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/sync/sales", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadAsAsync<SyncSalesResponseDto>();
            result!.Results.Should().HaveCount(2);

            var goodResult = result.Results.Single(r => r.ClientTransactionId == goodSale.ClientTransactionId);
            goodResult.Status.Should().Be(SyncSaleStatusConstants.Created);
            goodResult.OrderId.Should().NotBeNull();

            var badResult = result.Results.Single(r => r.ClientTransactionId == badSale.ClientTransactionId);
            badResult.Status.Should().Be(SyncSaleStatusConstants.Failed);
            badResult.OrderId.Should().BeNull();
            badResult.ErrorMessage.Should().Contain("Insufficient stock");
        }

        [Fact]
        public async Task SyncSales_ForCashierWithNoOpenShift_FailsWithClearReason()
        {
            // Arrange - close the cashier's only open shift first.
            var currentShiftResponse = await _clientCashier.GetAsync("api/shifts/current");
            currentShiftResponse.EnsureSuccessStatusCode();
            var currentShift = await currentShiftResponse.Content.ReadAsAsync<ShiftResponseDto>();
            var closeResponse = await _clientCashier.PostAsJsonAsync($"api/shifts/{currentShift!.Id}/close", new CloseShiftDto { ClosingCountedAmount = 100.00m });
            closeResponse.EnsureSuccessStatusCode();

            var sale = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 1, tenderAmount: 11.50m);
            var request = new SyncSalesRequestDto { Sales = new List<QueuedSaleDto> { sale } };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/sync/sales", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadAsAsync<SyncSalesResponseDto>();
            var entry = result!.Results.Single();
            entry.Status.Should().Be(SyncSaleStatusConstants.Failed);
            entry.ErrorMessage.Should().Contain("open shift");
        }

        [Fact]
        public async Task SyncSales_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var unauthClient = _factory.GetTestClient();
            var sale = ValidQueuedSale(Guid.NewGuid().ToString(), productId: 1, quantity: 1, tenderAmount: 11.50m);
            var request = new SyncSalesRequestDto { Sales = new List<QueuedSaleDto> { sale } };

            // Act
            var response = await unauthClient.PostAsJsonAsync("api/sync/sales", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            unauthClient.Dispose();
        }
    }
}
