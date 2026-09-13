import client from "./client";

function normalizeCustomer(c) {
  return {
    id: c.id,
    phone: c.phone,
    name: c.name,
    loyaltyPoints: c.loyaltyPoints,
    tier: c.tier,
  };
}

/** Returns null for a not-found phone rather than throwing - this is a lookup, not a validated write. */
export async function fetchCustomerByPhone(phone) {
  try {
    const { data } = await client.get(`/customers/${encodeURIComponent(phone)}`);
    return normalizeCustomer(data);
  } catch (err) {
    if (err.response?.status === 404) return null;
    throw err;
  }
}

export async function registerCustomer(phone, name) {
  const { data } = await client.post("/customers", { phone, name });
  return normalizeCustomer(data);
}

/** Positive points credits, negative debits. SuperAdmin only - the API rejects this for a Cashier token. */
export async function adjustLoyaltyPoints(phone, points, reason) {
  const { data } = await client.post(`/customers/${encodeURIComponent(phone)}/loyalty-adjustment`, {
    points,
    reason: reason || undefined,
  });
  return normalizeCustomer(data);
}

export async function fetchLoyaltyHistory(phone) {
  const { data } = await client.get(`/customers/${encodeURIComponent(phone)}/loyalty-history`);
  return data.items.map((t) => ({
    id: t.id,
    orderId: t.orderId,
    type: t.type,
    points: t.points,
    reason: t.reason,
    createdAt: t.createdAt,
  }));
}
