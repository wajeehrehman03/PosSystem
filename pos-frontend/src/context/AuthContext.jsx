import { createContext, useContext, useState, useCallback } from "react";
import { loginRequest } from "../api/auth";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem("pos_token"));
  const [user, setUser] = useState(() => {
    const stored = localStorage.getItem("pos_user");
    return stored ? JSON.parse(stored) : null;
  });

  const login = useCallback(async (username, password) => {
    const result = await loginRequest(username, password);
    localStorage.setItem("pos_token", result.accessToken);
    localStorage.setItem("pos_user", JSON.stringify(result.user));
    setToken(result.accessToken);
    setUser(result.user);
    return result.user;
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("pos_token");
    localStorage.removeItem("pos_user");
    setToken(null);
    setUser(null);
  }, []);

  const isSuperAdmin = user?.role === "SuperAdmin";

  return (
    <AuthContext.Provider value={{ token, user, login, logout, isSuperAdmin }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
