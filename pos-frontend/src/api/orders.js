import client from "./client";

function normalizeOrderSummary(o) {
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    orderDate: o.orderDate,
    totalAmount: o.totalAmount,
    itemCount: o.items.reduce((sum, item) => sum + item.quantity, 0),
  };
}

export async function fetchOrdersByCashier(cashierId) {
  const { data } = await client.get(`/orders/by-cashier/${cashierId}`);
  return data.items.map(normalizeOrderSummary);
}

function normalizeOrderDetail(o) {
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    orderDate: o.orderDate,
    subtotal: o.subtotal,
    taxAmount: o.taxAmount,
    totalAmount: o.totalAmount,
    changeDue: o.changeDue,
    items: o.items.map((i) => ({
      id: i.id,
      productId: i.productId,
      productName: i.productName,
      quantity: i.quantity,
      unitPrice: i.unitPrice,
      lineTotal: i.lineTotal,
      quantityRefunded: i.quantityRefunded,
    })),
  };
}

export async function fetchOrderDetail(orderId) {
  const { data } = await client.get(`/orders/${orderId}`);
  return normalizeOrderDetail(data);
}

export async function processRefund(orderId, { items, method, reason }) {
  const { data } = await client.post(`/orders/${orderId}/refund`, { items, method, reason: reason || undefined });
  return {
    id: data.id,
    subtotalRefunded: data.subtotalRefunded,
    taxRefunded: data.taxRefunded,
    totalRefunded: data.totalRefunded,
  };
}
