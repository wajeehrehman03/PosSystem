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
    /// Integration tests for CatalogController with RBAC authorization enforcement.
    /// </summary>
    public class CatalogControllerTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _clientAdmin = null!;
        private HttpClient _clientCashier = null!;
        private HttpClient _clientUnauthenticated = null!;

        public CatalogControllerTests()
        {
            _factory = new TestWebApplicationFactory();
        }

        public async Task InitializeAsync()
        {
            // Create authenticated clients for different roles
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
        }

        public async Task DisposeAsync()
        {
            _clientAdmin?.Dispose();
            _clientCashier?.Dispose();
            _clientUnauthenticated?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        #region Authorization Tests

        [Fact]
        public async Task GetAll_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _clientUnauthenticated.GetAsync("api/catalog");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetAll_WithCashierRole_ReturnsOk()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetAll_WithAdminRole_ReturnsOk()
        {
            // Act
            var response = await _clientAdmin.GetAsync("api/catalog");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetById_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _clientUnauthenticated.GetAsync("api/catalog/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetById_WithCashierRole_ReturnsOk()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var createDto = new { sku = "NEW-SKU", name = "New Product", price = 29.99, stockQuantity = 50 };

            // Act
            var response = await _clientUnauthenticated.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Create_WithCashierRole_ReturnsForbidden()
        {
            // Arrange
            var createDto = new { sku = "NEW-SKU-001", name = "New Product", price = 29.99, stockQuantity = 50 };

            // Act
            var response = await _clientCashier.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Create_WithAdminRole_SucceedsWithCreatedStatus()
        {
            // Arrange
            var createDto = new { sku = "ADMIN-PRODUCT-001", name = "Admin Created Product", price = 49.99m, stockQuantity = 100 };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Update_WithCashierRole_ReturnsForbidden()
        {
            // Arrange
            var updateDto = new { id = 1, sku = "SKU-001", name = "Updated Name", price = 15.00, stockQuantity = 100 };

            // Act
            var response = await _clientCashier.PutAsJsonAsync("api/catalog/1", updateDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Update_WithAdminRole_Succeeds()
        {
            // Arrange
            var updateDto = new { id = 1, sku = "SKU-001", name = "Admin Updated Name", price = 12.50m, stockQuantity = 150 };

            // Act
            var response = await _clientAdmin.PutAsJsonAsync("api/catalog/1", updateDto);

            // Assert - CatalogController.Update returns NoContent() on success
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Delete_WithCashierRole_ReturnsForbidden()
        {
            // Act
            var response = await _clientCashier.DeleteAsync("api/catalog/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Delete_WithAdminRole_Succeeds()
        {
            // Act
            var response = await _clientAdmin.DeleteAsync("api/catalog/1");

            // Assert - CatalogController.Delete returns NoContent() on success
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        #endregion

        #region Read Operation Tests

        [Fact]
        public async Task GetAll_ReturnsProductList()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((object)content!.items).Should().NotBeNull();
            ((int)content!.count).Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetById_WithValidId_ReturnsProduct()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<Product>();
            content.Should().NotBeNull();
            content!.Id.Should().Be(1);
        }

        [Fact]
        public async Task GetById_WithInvalidId_ReturnsNotFound()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetById_WithNegativeId_ReturnsBadRequest()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/-1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetLowStock_ReturnsProductsWithLowInventory()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/low-stock");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((object)content!.items).Should().NotBeNull();
            // Product 3 has 0 stock, which is low
            ((int)content!.count).Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task GetBySku_WithValidSku_ReturnsProduct()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/sku/SKU001");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsAsync<Product>();
            content.Should().NotBeNull();
            content!.Sku.Should().Be("SKU001");
        }

        [Fact]
        public async Task GetBySku_WithInvalidSku_ReturnsNotFound()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/sku/NONEXISTENT-SKU");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetBySku_CaseInsensitive_ReturnsProduct()
        {
            // Act - Product 1 has SKU "SKU001"
            var response = await _clientCashier.GetAsync("api/catalog/sku/sku001");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Write Operation Tests

        [Fact]
        public async Task Create_WithValidProduct_ReturnsCreatedWithProductData()
        {
            // Arrange
            var createDto = new
            {
                sku = "NEW-PRODUCT-SKU",
                name = "Brand New Product",
                price = 99.99m,
                stockQuantity = 200,
                minimumStockThreshold = 10
            };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var content = await response.Content.ReadAsAsync<dynamic>();
            ((string)content!.name).Should().Be("Brand New Product");
            ((string)content!.sku).Should().Be("NEW-PRODUCT-SKU");
            ((decimal)content!.price).Should().Be(99.99m);
        }

        [Fact]
        public async Task Create_WithDuplicateSku_ReturnsConflict()
        {
            // Arrange - SKU001 already exists
            var createDto = new { sku = "SKU001", name = "Duplicate Product", price = 10.00, stockQuantity = 50 };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert - CatalogManager.Add throws InvalidOperationException for a duplicate SKU,
            // which CatalogController.Create maps to 409 Conflict, not 400 BadRequest.
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Create_WithNegativePrice_ReturnsBadRequest()
        {
            // Arrange
            var createDto = new { sku = "NEGATIVE-PRICE", name = "Negative Price Product", price = -10.00, stockQuantity = 50 };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_WithZeroPrice_ReturnsBadRequest()
        {
            // Arrange
            var createDto = new { sku = "ZERO-PRICE", name = "Zero Price Product", price = 0, stockQuantity = 50 };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_WithoutSku_ReturnsBadRequest()
        {
            // Arrange
            var createDto = new { name = "No SKU Product", price = 10.00, stockQuantity = 50 };

            // Act
            var response = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Update_WithValidData_ReturnsOk()
        {
            // Arrange
            var updateDto = new { id = 1, sku = "SKU001", name = "Updated Product 1", price = 15.00m, stockQuantity = 150 };

            // Act
            var response = await _clientAdmin.PutAsJsonAsync("api/catalog/1", updateDto);

            // Assert - CatalogController.Update returns NoContent() on success
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Update_WithNonexistentId_ReturnsNotFound()
        {
            // Arrange
            var updateDto = new { id = 999, sku = "SKU-999", name = "Nonexistent Product", price = 10.00, stockQuantity = 50 };

            // Act
            var response = await _clientAdmin.PutAsJsonAsync("api/catalog/999", updateDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_WithValidId_ReturnsOk()
        {
            // Arrange - Create a product first, then delete it
            var createDto = new { sku = "DELETE-TEST-SKU", name = "Product to Delete", price = 10.00, stockQuantity = 50 };
            var createResponse = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);
            createResponse.EnsureSuccessStatusCode();
            var createdProduct = await createResponse.Content.ReadAsAsync<Product>();

            // Act
            var deleteResponse = await _clientAdmin.DeleteAsync($"api/catalog/{createdProduct!.Id}");

            // Assert - CatalogController.Delete returns NoContent() on success
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Delete_WithNonexistentId_ReturnsNotFound()
        {
            // Act
            var response = await _clientAdmin.DeleteAsync("api/catalog/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }


        #endregion

        #region Business Logic Tests

        [Fact]
        public async Task GetLowStock_WithoutAuthentication_ReturnsUnauthorized()
        {
            // Act
            var response = await _clientUnauthenticated.GetAsync("api/catalog/low-stock");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetLowStock_WithCashierRole_ReturnsOk()
        {
            // Act
            var response = await _clientCashier.GetAsync("api/catalog/low-stock");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Create_UpdateStockQuantity_ReflectsInGetById()
        {
            // Arrange
            var createDto = new { sku = "STOCK-TEST-SKU", name = "Stock Test Product", price = 10.00, stockQuantity = 100 };
            var createResponse = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);
            var createdProduct = await createResponse.Content.ReadAsAsync<Product>();

            // Act
            var getResponse = await _clientAdmin.GetAsync($"api/catalog/{createdProduct!.Id}");

            // Assert
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var retrievedProduct = await getResponse.Content.ReadAsAsync<Product>();
            retrievedProduct!.StockQuantity.Should().Be(100);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public async Task Scenario_CashierBrowsingCatalogAndAdminManagingProducts()
        {
            // Cashier: Get all products
            var cashierGetAll = await _clientCashier.GetAsync("api/catalog");
            cashierGetAll.StatusCode.Should().Be(HttpStatusCode.OK);

            // Cashier: View product details
            var cashierGetProduct = await _clientCashier.GetAsync("api/catalog/1");
            cashierGetProduct.StatusCode.Should().Be(HttpStatusCode.OK);

            // Cashier: Try to create product (should fail)
            var cashierCreate = await _clientCashier.PostAsJsonAsync("api/catalog", new { sku = "TEST", name = "Test", price = 10, stockQuantity = 10 });
            cashierCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Admin: Create new product
            var adminCreate = await _clientAdmin.PostAsJsonAsync("api/catalog", new { sku = "ADMIN-MANAGED-SKU", name = "Admin Product", price = 25.00, stockQuantity = 500 });
            adminCreate.StatusCode.Should().Be(HttpStatusCode.Created);

            // Both can now view it
            var adminGet = await _clientAdmin.GetAsync("api/catalog");
            adminGet.StatusCode.Should().Be(HttpStatusCode.OK);

            var cashierGet = await _clientCashier.GetAsync("api/catalog");
            cashierGet.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Scenario_InventoryManagement()
        {
            // Admin creates product
            var createDto = new { sku = "INV-MGMT-SKU", name = "Inventory Management Test", price = 30.00, stockQuantity = 100 };
            var createResponse = await _clientAdmin.PostAsJsonAsync("api/catalog", createDto);
            createResponse.EnsureSuccessStatusCode();
            var product = await createResponse.Content.ReadAsAsync<Product>();

            // Cashier views product
            var viewResponse = await _clientCashier.GetAsync($"api/catalog/{product!.Id}");
            viewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var viewedProduct = await viewResponse.Content.ReadAsAsync<Product>();
            viewedProduct!.StockQuantity.Should().Be(100);

            // Admin updates stock
            var updateDto = new { id = product.Id, sku = product.Sku, name = product.Name, price = product.Price, stockQuantity = 50 };
            var updateResponse = await _clientAdmin.PutAsJsonAsync($"api/catalog/{product.Id}", updateDto);
            // CatalogController.Update returns NoContent() on success
            updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Cashier verifies updated stock
            var verifyResponse = await _clientCashier.GetAsync($"api/catalog/{product.Id}");
            var verifiedProduct = await verifyResponse.Content.ReadAsAsync<Product>();
            verifiedProduct!.StockQuantity.Should().Be(50);
        }

        #endregion
    }
}
