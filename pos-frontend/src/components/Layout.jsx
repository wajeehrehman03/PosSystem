import { useEffect, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { fetchLowStockProducts } from "../api/catalog";

// How often to re-check for low stock while the app is open, so a SuperAdmin sees new alerts
// without needing to refresh the page - not push notifications, just periodic polling.
const LOW_STOCK_POLL_MS = 60000;

export default function Layout() {
  const { user, logout, isSuperAdmin } = useAuth();
  const [lowStockCount, setLowStockCount] = useState(0);

  useEffect(() => {
    if (!isSuperAdmin) return;

    let cancelled = false;

    const check = async () => {
      try {
        const items = await fetchLowStockProducts();
        if (!cancelled) setLowStockCount(items.length);
      } catch {
        // Silent - this is a background convenience check, not a page the user is looking at.
      }
    };

    check();
    const interval = setInterval(check, LOW_STOCK_POLL_MS);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [isSuperAdmin]);

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">POS System</div>
        <nav>
          <NavLink to="/" end>
            Catalog
          </NavLink>
          <NavLink to="/pos">Register</NavLink>
          <NavLink to="/customers">Customers</NavLink>
          <NavLink to="/shift">Shift</NavLink>
          {isSuperAdmin && <NavLink to="/staff">Staff</NavLink>}
          {isSuperAdmin && <NavLink to="/audit-log">Audit Log</NavLink>}
        </nav>
        <div className="user-info">
          {isSuperAdmin && lowStockCount > 0 && (
            <NavLink to="/" className="low-stock-alert" title="Products at or below their minimum stock threshold">
              &#9888; {lowStockCount} low stock
            </NavLink>
          )}
          <span className="badge">{user?.role}</span>
          <span>{user?.username}</span>
          <button onClick={logout}>Log out</button>
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
