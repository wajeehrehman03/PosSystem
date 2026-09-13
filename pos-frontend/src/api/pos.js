import client from "./client";

function normalizeCartItem(item) {
  return {
    id: item.id,
    productId: item.productId,
    quantity: item.quantity,
    subtotal: item.subtotal,
    product: item.product
      ? {
          id: item.product.id,
          sku: item.product.sku,
          name: item.product.name,
          price: item.product.price,
        }
      : null,
  };
}

export async function fetchCart() {
  const { data } = await client.get("/pos/cart");
  return data.items.map(normalizeCartItem);
}

export async function addToCart(productId, quantity) {
  await client.post("/pos/cart/add", { productId, quantity });
}

export async function undoCart() {
  await client.post("/pos/cart/undo");
}

export async function removeCartItem(cartItemId) {
  await client.delete(`/pos/cart/item/${cartItemId}`);
}

export async function clearCart() {
  await client.post("/pos/cart/clear");
}

export async function checkout({ cashierName, tenders, customerPhone, discountCode, pointsToRedeem }) {
  const { data } = await client.post("/pos/checkout", {
    cashierName: cashierName || undefined,
    tenders,
    customerPhone: customerPhone || undefined,
    discountCode: discountCode || undefined,
    pointsToRedeem: pointsToRedeem || undefined,
  });

  // The checkout response wrapper and its nested `order`/`payments` objects are built as
  // anonymous types in PosController.Checkout, so their keys are already camelCase.
  return {
    message: data.message,
    order: data.order,
    receipt: data.receipt,
  };
}
