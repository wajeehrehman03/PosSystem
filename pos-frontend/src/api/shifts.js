import client from "./client";

function normalizeShift(s) {
  return {
    id: s.id,
    cashierId: s.cashierId,
    registerCode: s.registerCode,
    openingFloat: s.openingFloat,
    openedAt: s.openedAt,
    closedAt: s.closedAt,
    closingCountedAmount: s.closingCountedAmount,
    status: s.status,
  };
}

function normalizeReport(r) {
  return {
    shiftId: r.shiftId,
    registerCode: r.registerCode,
    openingFloat: r.openingFloat,
    cashSalesTotal: r.cashSalesTotal,
    cashDropsTotal: r.cashDropsTotal,
    payInsTotal: r.payInsTotal,
    payOutsTotal: r.payOutsTotal,
    cashRefundsTotal: r.cashRefundsTotal,
    expectedCash: r.expectedCash,
    isFinal: r.isFinal,
    closingCountedAmount: r.closingCountedAmount,
    variance: r.variance,
    generatedAt: r.generatedAt,
  };
}

export async function fetchCurrentShift() {
  try {
    const { data } = await client.get("/shifts/current");
    return normalizeShift(data);
  } catch (err) {
    if (err.response?.status === 404) return null;
    throw err;
  }
}

export async function openShift(registerCode, openingFloat) {
  const { data } = await client.post("/shifts/open", { registerCode, openingFloat });
  return normalizeShift(data);
}

export async function fetchXReport(shiftId) {
  const { data } = await client.get(`/shifts/${shiftId}/x-report`);
  return normalizeReport(data);
}

export async function closeShift(shiftId, closingCountedAmount) {
  const { data } = await client.post(`/shifts/${shiftId}/close`, { closingCountedAmount });
  return normalizeReport(data);
}

function normalizeOrderSummary(o) {
  return {
    id: o.id,
    orderNumber: o.orderNumber,
    orderDate: o.orderDate,
    totalAmount: o.totalAmount,
    itemCount: o.items.reduce((sum, item) => sum + item.quantity, 0),
  };
}

export async function fetchShiftOrders(shiftId) {
  const { data } = await client.get(`/shifts/${shiftId}/orders`);
  return data.items.map(normalizeOrderSummary);
}
