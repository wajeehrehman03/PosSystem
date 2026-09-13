import client from "./client";

function normalizeEntry(a) {
  return {
    id: a.id,
    username: a.username,
    action: a.action,
    entityType: a.entityType,
    entityId: a.entityId,
    details: a.details,
    createdAt: a.createdAt,
  };
}

export async function fetchAuditLog() {
  const { data } = await client.get("/audit-log");
  return data.items.map(normalizeEntry);
}
