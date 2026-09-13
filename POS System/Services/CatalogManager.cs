using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using PosWebApi.Data;
using PosWebApi.Models;

namespace PosWebApi.Services
{
    public class CatalogManager : GenericRepository<Product>
    {
        private readonly IDistributedCache _cache;

        private static readonly DistributedCacheEntryOptions CacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        };

        private const string AllProductsCacheKey = "products:all";
        private static string ProductCacheKey(int id) => $"product:{id}";

        public CatalogManager(AppDbContext context, IDistributedCache cache) : base(context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Cached read of every product, for the read-only "list catalog" endpoint. NOT used by
        /// any write path (Add/Update/Delete/DeductStock) - those need the tracked entity from
        /// the DbContext directly (see GetById/GetAll, inherited unchanged from
        /// GenericRepository), since a cache hit returns a freshly-deserialized, untracked
        /// instance that EF would silently ignore any mutation to.
        /// </summary>
        public IEnumerable<Product> GetAllCached()
        {
            var cached = _cache.GetString(AllProductsCacheKey);
            if (cached != null)
                return JsonConvert.DeserializeObject<List<Product>>(cached) ?? new List<Product>();

            var products = GetAll().ToList();
            _cache.SetString(AllProductsCacheKey, JsonConvert.SerializeObject(products), CacheEntryOptions);
            return products;
        }

        /// <summary>
        /// Cached read of a single product by ID, for the read-only "get product" endpoint. See
        /// GetAllCached's remarks - not used by any write path.
        /// </summary>
        public Product? GetByIdCached(int id)
        {
            if (id <= 0)
                return null;

            var key = ProductCacheKey(id);
            var cached = _cache.GetString(key);
            if (cached != null)
                return JsonConvert.DeserializeObject<Product>(cached);

            var product = GetById(id);
            if (product != null)
                _cache.SetString(key, JsonConvert.SerializeObject(product), CacheEntryOptions);

            return product;
        }

        /// <summary>
        /// Evicts the cache entries affected by a write. Always called only after the write's own
        /// SaveChanges has already committed successfully - a failed write must never evict a
        /// cache entry for data that's actually still correct, and must never leave a stale entry
        /// for data that was never persisted.
        /// </summary>
        private void InvalidateProductCache(int? productId)
        {
            if (productId.HasValue && productId.Value > 0)
                _cache.Remove(ProductCacheKey(productId.Value));

            _cache.Remove(AllProductsCacheKey);
        }

        public bool CheckStock(int productId, int quantity)
        {
            if (productId <= 0 || quantity <= 0)
                return false;

            var product = GetById(productId);
            return product != null && product.StockQuantity >= quantity;
        }

        public void DeductStock(int productId, int quantity)
        {
            if (productId <= 0 || quantity <= 0)
                throw new ArgumentException("Product ID and quantity must be positive");

            var product = GetById(productId);
            if (product == null)
                throw new InvalidOperationException($"Product with ID {productId} not found");

            if (product.StockQuantity < quantity)
                throw new InvalidOperationException($"Insufficient stock for product {productId}. Available: {product.StockQuantity}, Requested: {quantity}");

            product.StockQuantity -= quantity;

            // If a transaction is already active (e.g. PosEngine.Checkout calls DeductStock once
            // per cart line inside its own ambient transaction), just save and let that outer
            // transaction own the commit/rollback. Relational providers throw when BeginTransaction
            // is called a second time on a connection that already has an active transaction, so
            // starting a nested one here would break checkout against real MySQL.
            if (_context.Database.CurrentTransaction != null)
            {
                _context.SaveChanges();
                InvalidateProductCache(productId);
                return;
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    _context.SaveChanges();
                    transaction.Commit();
                    InvalidateProductCache(productId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Adds stock back (a refunded return, a delivery, etc). Mirrors DeductStock's
        /// transaction-awareness and cache invalidation exactly, just in the opposite direction -
        /// RefundService relies on this rather than mutating Product.StockQuantity directly, so a
        /// restock can never leave the Redis-cached read stale.
        /// </summary>
        public void RestockProduct(int productId, int quantity)
        {
            if (productId <= 0 || quantity <= 0)
                throw new ArgumentException("Product ID and quantity must be positive");

            var product = GetById(productId);
            if (product == null)
                throw new InvalidOperationException($"Product with ID {productId} not found");

            product.StockQuantity += quantity;

            if (_context.Database.CurrentTransaction != null)
            {
                _context.SaveChanges();
                InvalidateProductCache(productId);
                return;
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    _context.SaveChanges();
                    transaction.Commit();
                    InvalidateProductCache(productId);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public IEnumerable<Product> GetLowStockProducts()
        {
            return _dbSet.Where(p => p.StockQuantity <= p.MinimumStockThreshold).ToList();
        }

        public bool IsSkuUnique(string sku, int? excludeProductId = null)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return false;

            var query = _dbSet.Where(p => p.Sku.ToLower() == sku.ToLower());

            if (excludeProductId.HasValue)
                query = query.Where(p => p.Id != excludeProductId.Value);

            return !query.Any();
        }

        public Product? GetBySku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return null;

            return _dbSet.FirstOrDefault(p => p.Sku.ToLower() == sku.ToLower());
        }

        public override void Add(Product entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrWhiteSpace(entity.Sku))
                throw new ArgumentException("Product SKU is required");

            if (!IsSkuUnique(entity.Sku))
                throw new InvalidOperationException($"A product with SKU '{entity.Sku}' already exists");

            if (string.IsNullOrWhiteSpace(entity.Name))
                throw new ArgumentException("Product name is required");

            if (entity.Price <= 0)
                throw new ArgumentException("Product price cannot be zero or negative");

            base.Add(entity);
            InvalidateProductCache(entity.Id);
        }

        public override void Update(Product entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.Id <= 0)
                throw new ArgumentException("Product ID must be valid");

            if (string.IsNullOrWhiteSpace(entity.Sku))
                throw new ArgumentException("Product SKU is required");

            if (!IsSkuUnique(entity.Sku, entity.Id))
                throw new InvalidOperationException($"A product with SKU '{entity.Sku}' already exists");

            if (string.IsNullOrWhiteSpace(entity.Name))
                throw new ArgumentException("Product name is required");

            if (entity.Price <= 0)
                throw new ArgumentException("Product price cannot be zero or negative");

            base.Update(entity);
            InvalidateProductCache(entity.Id);
        }

        public override bool Delete(int id)
        {
            // Any exception from base.Delete (including the FK-restrict InvalidOperationException
            // CatalogController specifically handles for a product with order history) propagates
            // unchanged - only a genuinely successful delete invalidates the cache.
            bool deleted = base.Delete(id);
            if (deleted)
                InvalidateProductCache(id);

            return deleted;
        }
    }
}
