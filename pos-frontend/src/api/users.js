import client from "./client";

function normalizeUser(u) {
  return {
    id: u.id,
    username: u.username,
    email: u.email,
    role: u.role,
    isActive: u.isActive,
    createdAt: u.createdAt,
    lastLoginAt: u.lastLoginAt,
  };
}

export async function fetchUsers() {
  const { data } = await client.get("/auth/users");
  return data.items.map(normalizeUser);
}

export async function createUser({ username, email, password, confirmPassword, role }) {
  const { data } = await client.post("/auth/register", {
    username,
    email,
    password,
    confirmPassword,
    role,
  });
  return data;
}

export async function deactivateUser(id) {
  const { data } = await client.patch(`/auth/users/${id}/deactivate`);
  return normalizeUser(data);
}

export async function activateUser(id) {
  const { data } = await client.patch(`/auth/users/${id}/activate`);
  return normalizeUser(data);
}

export async function deleteUser(id) {
  await client.delete(`/auth/users/${id}`);
}
