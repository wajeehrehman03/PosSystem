import client from "./client";

// Program.cs registers AddNewtonsoftJson(), which defaults to a camelCase contract resolver -
// so every response from this API (both strongly-typed DTOs and inline anonymous objects) comes
// back camelCased, confirmed against the running API rather than assumed.

export async function loginRequest(username, password) {
  const { data } = await client.post("/auth/login", { username, password });
  return {
    accessToken: data.accessToken,
    tokenType: data.tokenType,
    expiresIn: data.expiresIn,
    user: {
      id: data.user.id,
      username: data.user.username,
      email: data.user.email,
      role: data.user.role,
    },
  };
}

export async function registerRequest({ username, email, password, confirmPassword, role }) {
  const { data } = await client.post("/auth/register", {
    username,
    email,
    password,
    confirmPassword,
    role: role || undefined,
  });
  return data;
}
