using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using PosWebApi.Models;
using PosWebApi.Services;

namespace PosWebApi.Controllers
{
    /// <summary>
    /// Product catalog management endpoints.
    /// Read operations are available to authenticated users (SuperAdmin and Cashier).
    /// Write operations (Create, Update, Delete) are restricted to SuperAdmin users only.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogController : ControllerBase
    {
        private readonly CatalogManager _catalog;
        private readonly AuditService _auditService;

        public CatalogController(CatalogManager catalog, AuditService auditService)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Retrieves all products in the catalog.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <returns>List of all products</returns>
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult GetAll()
        {
            try
            {
                var products = _catalog.GetAllCached();
                return Ok(new { items = products, count = products.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve products", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a specific product by ID.
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <param name="id">The product ID to retrieve</param>
        /// <returns>Product information if found</returns>
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult GetById(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Product ID must be greater than 0" });

                var product = _catalog.GetByIdCached(id);
                if (product == null)
                    return NotFound(new { error = $"Product with ID {id} not found" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve product", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves all products with low stock (below minimum threshold).
        /// Requires authentication with SuperAdmin or Cashier role.
        /// </summary>
        /// <returns>List of products with low stock</returns>
        [HttpGet("low-stock")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult GetLowStock()
        {
            try
            {
                var products = _catalog.GetLowStockProducts();
                return Ok(new { items = products, count = products.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve low stock products", details = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a product by its SKU (Stock Keeping Unit).
        /// Requires authentication with Cashier or SuperAdmin role.
        /// </summary>
        /// <param name="sku">The product SKU to search for</param>
        /// <returns>Product information if found</returns>
        [HttpGet("sku/{sku}")]
        [Authorize(Roles = "SuperAdmin,Cashier")]
        public IActionResult GetBySku(string sku)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sku))
                    return BadRequest(new { error = "SKU is required" });

                var product = _catalog.GetBySku(sku);
                if (product == null)
                    return NotFound(new { error = $"Product with SKU '{sku}' not found" });

                return Ok(product);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve product by SKU", details = ex.Message });
            }
        }

        /// <summary>
        /// Creates a new product in the catalog.
        /// RESTRICTED: Only SuperAdmin users can create products.
        /// </summary>
        /// <param name="product">Product details to create</param>
        /// <returns>Created product with 201 status code</returns>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Create([FromBody] Product product)
        {
            try
            {
                if (product == null)
                    return BadRequest(new { error = "Request body is required" });

                if (!ModelState.IsValid)
                    return BadRequest(new { error = "Invalid input", details = ModelState });

                if (string.IsNullOrWhiteSpace(product.Sku))
                    return BadRequest(new { error = "Product SKU is required" });

                if (string.IsNullOrWhiteSpace(product.Name))
                    return BadRequest(new { error = "Product name is required" });

                if (product.Price <= 0)
                    return BadRequest(new { error = "Product price cannot be zero or negative" });

                if (product.StockQuantity < 0)
                    return BadRequest(new { error = "Stock quantity cannot be negative" });

                if (product.MinimumStockThreshold < 0)
                    return BadRequest(new { error = "Minimum stock threshold cannot be negative" });

                _catalog.Add(product);
                _auditService.Log("ProductCreated", "Product", product.Id, $"{product.Sku}: {product.Name} @ ${product.Price:F2}");
                return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create product", details = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing product in the catalog.
        /// RESTRICTED: Only SuperAdmin users can update products.
        /// </summary>
        /// <param name="id">The product ID to update</param>
        /// <param name="product">Updated product details</param>
        /// <returns>No content (204) on success</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Update(int id, [FromBody] Product product)
        {
            try
            {
                if (product == null)
                    return BadRequest(new { error = "Request body is required" });

                if (id <= 0)
                    return BadRequest(new { error = "Product ID must be greater than 0" });

                if (id != product.Id)
                    return BadRequest(new { error = "Product ID in URL does not match ID in body" });

                if (!ModelState.IsValid)
                    return BadRequest(new { error = "Invalid input", details = ModelState });

                if (string.IsNullOrWhiteSpace(product.Sku))
                    return BadRequest(new { error = "Product SKU is required" });

                if (string.IsNullOrWhiteSpace(product.Name))
                    return BadRequest(new { error = "Product name is required" });

                if (product.Price <= 0)
                    return BadRequest(new { error = "Product price cannot be zero or negative" });

                if (product.StockQuantity < 0)
                    return BadRequest(new { error = "Stock quantity cannot be negative" });

                if (product.MinimumStockThreshold < 0)
                    return BadRequest(new { error = "Minimum stock threshold cannot be negative" });

                var existing = _catalog.GetById(id);
                if (existing == null)
                    return NotFound(new { error = $"Product with ID {id} not found" });

                // Apply changes onto the already-tracked instance rather than calling
                // Update() with the separately-deserialized `product`. Passing a second
                // instance with the same key makes EF Core throw "another instance with
                // the same key value is already being tracked" for every request.
                var oldPrice = existing.Price;
                existing.Sku = product.Sku;
                existing.Name = product.Name;
                existing.Price = product.Price;
                existing.StockQuantity = product.StockQuantity;
                existing.MinimumStockThreshold = product.MinimumStockThreshold;

                _catalog.Update(existing);

                if (oldPrice != product.Price)
                    _auditService.Log("ProductPriceChanged", "Product", id, $"{existing.Sku}: ${oldPrice:F2} -> ${product.Price:F2}");
                else
                    _auditService.Log("ProductUpdated", "Product", id, $"{existing.Sku} updated");

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to update product", details = ex.Message });
            }
        }

        /// <summary>
        /// Deletes a product from the catalog.
        /// RESTRICTED: Only SuperAdmin users can delete products.
        /// </summary>
        /// <param name="id">The product ID to delete</param>
        /// <returns>No content (204) on success</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public IActionResult Delete(int id)
        {
            try
            {
                if (id <= 0)
                    return BadRequest(new { error = "Product ID must be greater than 0" });

                var productToDelete = _catalog.GetById(id);

                if (_catalog.Delete(id))
                {
                    _auditService.Log("ProductDeleted", "Product", id,
                        productToDelete != null ? $"{productToDelete.Sku}: {productToDelete.Name}" : null);
                    return NoContent();
                }

                return NotFound(new { error = $"Product with ID {id} not found" });
            }
            // GenericRepository.Delete wraps a MySQL FK-restrict violation (deleting a product
            // that has historical OrderItem rows referencing it) as an InvalidOperationException
            // whose InnerException is the DbUpdateException/MySqlException pair - confirmed
            // empirically (ErrorCode 1451, RowIsReferenced2) rather than assumed. That's a real,
            // actionable conflict, not a server error.
            catch (InvalidOperationException ex) when (ex.InnerException is DbUpdateException dbEx
                && dbEx.InnerException is MySqlException mysqlEx
                && mysqlEx.ErrorCode == MySqlErrorCode.RowIsReferenced2)
            {
                return Conflict(new { error = "Cannot delete a product with existing order history." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to delete product", details = ex.Message });
            }
        }
    }
}
