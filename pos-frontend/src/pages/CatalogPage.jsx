import { useEffect, useState } from "react";
import { fetchProducts, createProduct, updateProduct, deleteProduct } from "../api/catalog";
import { useAuth } from "../context/AuthContext";

const emptyForm = { sku: "", name: "", price: "", stockQuantity: "", minimumStockThreshold: "5" };

export default function CatalogPage() {
  const { isSuperAdmin } = useAuth();
  const [products, setProducts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);

  const loadProducts = async () => {
    setLoading(true);
    setError("");
    try {
      const items = await fetchProducts();
      setProducts(items);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load catalog");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProducts();
  }, []);

  const resetForm = () => {
    setForm(emptyForm);
    setEditingId(null);
  };

  const startEdit = (product) => {
    setForm({
      sku: product.sku,
      name: product.name,
      price: String(product.price),
      stockQuantity: String(product.stockQuantity),
      minimumStockThreshold: String(product.minimumStockThreshold),
    });
    setEditingId(product.id);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    const payload = {
      sku: form.sku,
      name: form.name,
      price: Number(form.price),
      stockQuantity: Number(form.stockQuantity),
      minimumStockThreshold: Number(form.minimumStockThreshold),
    };

    try {
      if (editingId) {
        await updateProduct(editingId, payload);
      } else {
        await createProduct(payload);
      }
      resetForm();
      await loadProducts();
    } catch (err) {
      setError(err.response?.data?.error || "Save failed");
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm("Delete this product?")) return;
    setError("");
    try {
      await deleteProduct(id);
      await loadProducts();
    } catch (err) {
      setError(err.response?.data?.error || "Delete failed");
    }
  };

  return (
    <div>
      <h2>Product Catalog</h2>
      {error && <div className="error-banner">{error}</div>}

      {isSuperAdmin && (
        <form className="inline-form" onSubmit={handleSubmit}>
          <input
            placeholder="SKU"
            value={form.sku}
            onChange={(e) => setForm({ ...form, sku: e.target.value })}
            required
          />
          <input
            placeholder="Name"
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            required
          />
          <input
            type="number"
            step="0.01"
            min="0.01"
            placeholder="Price"
            value={form.price}
            onChange={(e) => setForm({ ...form, price: e.target.value })}
            required
          />
          <input
            type="number"
            placeholder="Stock"
            value={form.stockQuantity}
            onChange={(e) => setForm({ ...form, stockQuantity: e.target.value })}
            required
          />
          <input
            type="number"
            placeholder="Min threshold"
            value={form.minimumStockThreshold}
            onChange={(e) => setForm({ ...form, minimumStockThreshold: e.target.value })}
            required
          />
          <button type="submit">{editingId ? "Update" : "Add product"}</button>
          {editingId && (
            <button type="button" onClick={resetForm}>
              Cancel
            </button>
          )}
        </form>
      )}

      {loading ? (
        <p>Loading...</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Name</th>
              <th>Price</th>
              <th>Stock</th>
              <th>Min</th>
              {isSuperAdmin && <th></th>}
            </tr>
          </thead>
          <tbody>
            {products.map((p) => (
              <tr key={p.id} className={p.stockQuantity <= p.minimumStockThreshold ? "low-stock" : ""}>
                <td>{p.sku}</td>
                <td>{p.name}</td>
                <td>${p.price.toFixed(2)}</td>
                <td>{p.stockQuantity}</td>
                <td>{p.minimumStockThreshold}</td>
                {isSuperAdmin && (
                  <td className="actions">
                    <button onClick={() => startEdit(p)}>Edit</button>
                    <button onClick={() => handleDelete(p.id)} className="danger">
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            ))}
            {products.length === 0 && (
              <tr>
                <td colSpan={isSuperAdmin ? 6 : 5} className="empty">
                  No products yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
