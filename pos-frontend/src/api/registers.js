import client from "./client";

function normalizeRegister(r) {
  return {
    id: r.id,
    code: r.code,
    name: r.name,
    isActive: r.isActive,
    createdAt: r.createdAt,
  };
}

export async function fetchActiveRegisters() {
  const { data } = await client.get("/registers/active");
  return data.items.map(normalizeRegister);
}

export async function fetchAllRegisters() {
  const { data } = await client.get("/registers");
  return data.items.map(normalizeRegister);
}

export async function createRegister(code, name) {
  const { data } = await client.post("/registers", { code, name: name || undefined });
  return normalizeRegister(data);
}

export async function deactivateRegister(code) {
  await client.patch(`/registers/${encodeURIComponent(code)}/deactivate`);
}

export async function activateRegister(code) {
  await client.patch(`/registers/${encodeURIComponent(code)}/activate`);
}
