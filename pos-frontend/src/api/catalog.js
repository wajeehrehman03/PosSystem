import client from "./client";

function normalizeProduct(p) {
  return {
    id: p.id,
    sku: p.sku,
    name: p.name,
    price: p.price,
    stockQuantity: p.stockQuantity,
    minimumStockThreshold: p.minimumStockThreshold,
  };
}

export async function fetchProducts() {
  const { data } = await client.get("/catalog");
  return data.items.map(normalizeProduct);
}

export async function createProduct(product) {
  const { data } = await client.post("/catalog", {
    sku: product.sku,
    name: product.name,
    price: product.price,
    stockQuantity: product.stockQuantity,
    minimumStockThreshold: product.minimumStockThreshold,
  });
  return normalizeProduct(data);
}

export async function updateProduct(id, product) {
  await client.put(`/catalog/${id}`, {
    id,
    sku: product.sku,
    name: product.name,
    price: product.price,
    stockQuantity: product.stockQuantity,
    minimumStockThreshold: product.minimumStockThreshold,
  });
}

export async function deleteProduct(id) {
  await client.delete(`/catalog/${id}`);
}

export async function fetchLowStockProducts() {
  const { data } = await client.get("/catalog/low-stock");
  return data.items.map(normalizeProduct);
}
