import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { fetchProducts } from "../api/catalog";
import { fetchCart, addToCart, undoCart, removeCartItem, clearCart, checkout } from "../api/pos";
import { fetchCurrentShift } from "../api/shifts";
import { fetchCustomerByPhone } from "../api/customers";

// Mirrors Helpers/TaxHelper.CalculateTax's flat rate, so the cashier sees the true taxed total
// before checkout instead of just the pre-tax subtotal. This preview ignores discount codes and
// loyalty points (those need server-side lookups a Cashier's token can't make - discount code
// details are a SuperAdmin-only endpoint), so the actual checkout total may come back lower.
const TAX_RATE = 0.15;

const emptyCheckoutForm = {
  tenderMethod: "Cash",
  customerPhone: "",
  discountCode: "",
  pointsToRedeem: "",
};

export default function PosPage() {
  const [products, setProducts] = useState([]);
  const [cart, setCart] = useState([]);
  const [shift, setShift] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [result, setResult] = useState(null);
  const [checkoutForm, setCheckoutForm] = useState(emptyCheckoutForm);
  // Empty string means "use the computed total" - kept separate from checkoutForm so switching
  // to Card/GiftCard (which must tender exactly the total) never has to guess whether a stored
  // value was auto-computed or something the cashier deliberately typed for cash change.
  const [customTenderAmount, setCustomTenderAmount] = useState("");
  // Live lookup of the entered customer phone, so the cashier can see the loyalty balance
  // available to redeem before typing a points amount. "idle" = no phone entered yet.
  const [lookupStatus, setLookupStatus] = useState("idle");
  const [lookupCustomer, setLookupCustomer] = useState(null);

  const subtotal = cart.reduce((sum, item) => sum + item.subtotal, 0);
  const estimatedTax = subtotal * TAX_RATE;
  const estimatedTotal = subtotal + estimatedTax;

  const isCash = checkoutForm.tenderMethod === "Cash";
  const tenderAmount = isCash && customTenderAmount !== "" ? Number(customTenderAmount) : estimatedTotal;

  const refresh = async () => {
    setError("");
    try {
      const [productList, cartItems, currentShift] = await Promise.all([
        fetchProducts(),
        fetchCart(),
        fetchCurrentShift(),
      ]);
      setProducts(productList);
      setCart(cartItems);
      setShift(currentShift);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load register data");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, []);

  // Debounced so a lookup only fires once the cashier pauses typing, not on every keystroke.
  useEffect(() => {
    const phone = checkoutForm.customerPhone.trim();
    if (!phone) {
      setLookupStatus("idle");
      setLookupCustomer(null);
      return;
    }

    setLookupStatus("checking");
    const timer = setTimeout(async () => {
      try {
        const customer = await fetchCustomerByPhone(phone);
        setLookupCustomer(customer);
        setLookupStatus(customer ? "found" : "not-found");
      } catch {
        setLookupCustomer(null);
        setLookupStatus("error");
      }
    }, 400);

    return () => clearTimeout(timer);
  }, [checkoutForm.customerPhone]);

  const handleAdd = async (productId) => {
    setError("");
    try {
      await addToCart(productId, 1);
      await refresh();
    } catch (err) {
      setError(err.response?.data?.error || "Could not add to cart");
    }
  };

  const handleUndo = async () => {
    setError("");
    try {
      await undoCart();
      await refresh();
    } catch (err) {
      setError(err.response?.data?.error || "Could not undo");
    }
  };

  const handleRemoveItem = async (cartItemId) => {
    setError("");
    try {
      await removeCartItem(cartItemId);
      await refresh();
    } catch (err) {
      setError(err.response?.data?.error || "Could not remove item");
    }
  };

  const handleClear = async () => {
    setError("");
    try {
      await clearCart();
      await refresh();
    } catch (err) {
      setError(err.response?.data?.error || "Could not clear cart");
    }
  };

  const handleCheckout = async (e) => {
    e.preventDefault();
    setError("");
    setResult(null);

    if (!shift) {
      setError("Open a shift before checking out.");
      return;
    }

    try {
      const outcome = await checkout({
        tenders: [{ method: checkoutForm.tenderMethod, amount: tenderAmount }],
        customerPhone: checkoutForm.customerPhone || undefined,
        discountCode: checkoutForm.discountCode || undefined,
        pointsToRedeem: checkoutForm.pointsToRedeem ? Number(checkoutForm.pointsToRedeem) : undefined,
      });
      setResult(outcome);
      setCheckoutForm(emptyCheckoutForm);
      setCustomTenderAmount("");
      setLookupStatus("idle");
      setLookupCustomer(null);
      await refresh();
    } catch (err) {
      setError(err.response?.data?.error || "Checkout failed");
    }
  };

  if (loading) return <p>Loading register...</p>;

  return (
    <div className="pos-layout">
      <div className="pos-products">
        <h2>Products</h2>
        {!shift && (
          <div className="warning-banner">
            No open shift. <Link to="/shift">Open one</Link> before checking out.
          </div>
        )}
        {error && <div className="error-banner">{error}</div>}
        <div className="product-grid">
          {products.map((p) => (
            <button
              key={p.id}
              className="product-tile"
              onClick={() => handleAdd(p.id)}
              disabled={p.stockQuantity <= 0}
            >
              <strong>{p.name}</strong>
              <span>${p.price.toFixed(2)}</span>
              <small>{p.stockQuantity} in stock</small>
            </button>
          ))}
          {products.length === 0 && <p>No products in the catalog yet.</p>}
        </div>
      </div>

      <div className="pos-cart">
        <h2>Cart</h2>
        <ul className="cart-list">
          {cart.map((item) => (
            <li key={item.id}>
              <span className="cart-item-info">
                <span>
                  {item.product?.name} x{item.quantity}
                </span>
                <span>${item.subtotal.toFixed(2)}</span>
              </span>
              <button
                type="button"
                className="danger cart-item-remove"
                title="Remove this item"
                onClick={() => handleRemoveItem(item.id)}
              >
                &times;
              </button>
            </li>
          ))}
          {cart.length === 0 && <li className="empty">Cart is empty</li>}
        </ul>

        <div className="cart-totals">
          <div>
            <span>Subtotal</span>
            <span>${subtotal.toFixed(2)}</span>
          </div>
          <div>
            <span>Tax (15%)</span>
            <span>${estimatedTax.toFixed(2)}</span>
          </div>
          <div className="cart-total">
            <span>Total due</span>
            <span>${estimatedTotal.toFixed(2)}</span>
          </div>
        </div>
        {(checkoutForm.discountCode || checkoutForm.pointsToRedeem) && (
          <p className="hint">A discount code or redeemed points may lower this total further at checkout.</p>
        )}

        <div className="cart-actions">
          <button onClick={handleUndo} disabled={cart.length === 0}>
            Undo last
          </button>
          <button onClick={handleClear} disabled={cart.length === 0}>
            Clear cart
          </button>
        </div>

        <form className="checkout-form" onSubmit={handleCheckout}>
          <h3>Checkout</h3>
          <label>
            Payment method
            <select
              value={checkoutForm.tenderMethod}
              onChange={(e) => setCheckoutForm({ ...checkoutForm, tenderMethod: e.target.value })}
            >
              <option value="Cash">Cash</option>
              <option value="Card">Card</option>
              <option value="GiftCard">Gift Card</option>
            </select>
          </label>
          <label>
            Amount tendered
            <input
              type="number"
              step="0.01"
              placeholder={estimatedTotal.toFixed(2)}
              value={isCash ? customTenderAmount : estimatedTotal.toFixed(2)}
              onChange={(e) => setCustomTenderAmount(e.target.value)}
              disabled={!isCash}
            />
            <small>
              {isCash
                ? "Auto-filled with the total due - edit only if the customer hands over a different cash amount."
                : "Card/gift card tenders can't exceed the total, so this is locked to it."}
            </small>
          </label>
          <label>
            Customer phone (optional)
            <input
              value={checkoutForm.customerPhone}
              onChange={(e) => setCheckoutForm({ ...checkoutForm, customerPhone: e.target.value })}
            />
            {lookupStatus === "checking" && <small>Looking up customer...</small>}
            {lookupStatus === "found" && (
              <small>
                {lookupCustomer.name} - {lookupCustomer.loyaltyPoints} loyalty points available
              </small>
            )}
            {lookupStatus === "not-found" && <small>No customer found with this phone.</small>}
          </label>
          <label>
            Discount code (optional)
            <input
              value={checkoutForm.discountCode}
              onChange={(e) => setCheckoutForm({ ...checkoutForm, discountCode: e.target.value })}
            />
          </label>
          <label>
            Loyalty points to redeem (optional)
            <input
              type="number"
              min="0"
              max={lookupStatus === "found" ? lookupCustomer.loyaltyPoints : undefined}
              value={checkoutForm.pointsToRedeem}
              onChange={(e) => setCheckoutForm({ ...checkoutForm, pointsToRedeem: e.target.value })}
              disabled={lookupStatus !== "found"}
            />
            <small>
              {lookupStatus === "found"
                ? `Up to ${lookupCustomer.loyaltyPoints} points available for ${lookupCustomer.name}.`
                : "Enter a registered customer's phone number above to redeem points."}
            </small>
          </label>
          <button type="submit" disabled={cart.length === 0 || !shift}>
            Complete sale
          </button>
        </form>

        {result && (
          <div className="receipt-box">
            <h3>{result.message}</h3>
            <table className="data-table">
              <tbody>
                <tr>
                  <td>Subtotal</td>
                  <td>${result.order.subtotal.toFixed(2)}</td>
                </tr>
                {result.order.discountAmount > 0 && (
                  <tr>
                    <td>Discount</td>
                    <td>-${result.order.discountAmount.toFixed(2)}</td>
                  </tr>
                )}
                <tr>
                  <td>Tax</td>
                  <td>${result.order.tax.toFixed(2)}</td>
                </tr>
                <tr>
                  <td>
                    <strong>Total</strong>
                  </td>
                  <td>
                    <strong>${result.order.total.toFixed(2)}</strong>
                  </td>
                </tr>
                {result.order.changeDue > 0 && (
                  <tr>
                    <td>Change due</td>
                    <td>${result.order.changeDue.toFixed(2)}</td>
                  </tr>
                )}
                {result.order.pointsEarned > 0 && (
                  <tr>
                    <td>Loyalty points earned</td>
                    <td>{result.order.pointsEarned}</td>
                  </tr>
                )}
                {result.order.pointsRedeemed > 0 && (
                  <tr>
                    <td>Loyalty points redeemed</td>
                    <td>{result.order.pointsRedeemed}</td>
                  </tr>
                )}
                {result.order.customerLoyaltyBalance != null && (
                  <tr>
                    <td>Customer loyalty balance</td>
                    <td>{result.order.customerLoyaltyBalance}</td>
                  </tr>
                )}
              </tbody>
            </table>
            <details>
              <summary>Printable receipt</summary>
              <pre>{result.receipt}</pre>
            </details>
          </div>
        )}
      </div>
    </div>
  );
}
