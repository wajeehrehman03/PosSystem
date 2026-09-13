import { useEffect, useState } from "react";
import { fetchUsers, createUser, deactivateUser, activateUser, deleteUser } from "../api/users";
import { fetchOrdersByCashier } from "../api/orders";
import { fetchAllRegisters, createRegister, deactivateRegister, activateRegister } from "../api/registers";

const emptyForm = { username: "", email: "", password: "", confirmPassword: "" };
const emptyRegisterForm = { code: "", name: "" };

export default function StaffPage() {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [form, setForm] = useState(emptyForm);
  const [creating, setCreating] = useState(false);

  const [selectedUser, setSelectedUser] = useState(null);
  const [orders, setOrders] = useState([]);
  const [ordersLoading, setOrdersLoading] = useState(false);

  const [registers, setRegisters] = useState([]);
  const [registersLoading, setRegistersLoading] = useState(true);
  const [registerForm, setRegisterForm] = useState(emptyRegisterForm);
  const [creatingRegister, setCreatingRegister] = useState(false);

  const salesTotal = orders.reduce((sum, order) => sum + order.totalAmount, 0);

  const loadUsers = async () => {
    setLoading(true);
    setError("");
    try {
      const items = await fetchUsers();
      setUsers(items);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load staff accounts");
    } finally {
      setLoading(false);
    }
  };

  const loadRegisters = async () => {
    setRegistersLoading(true);
    setError("");
    try {
      const items = await fetchAllRegisters();
      setRegisters(items);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load registers");
    } finally {
      setRegistersLoading(false);
    }
  };

  useEffect(() => {
    loadUsers();
    loadRegisters();
  }, []);

  const handleCreateRegister = async (e) => {
    e.preventDefault();
    setError("");
    setCreatingRegister(true);
    try {
      await createRegister(registerForm.code, registerForm.name);
      setRegisterForm(emptyRegisterForm);
      await loadRegisters();
    } catch (err) {
      setError(err.response?.data?.error || "Could not create register");
    } finally {
      setCreatingRegister(false);
    }
  };

  const handleDeactivateRegister = async (code) => {
    setError("");
    try {
      await deactivateRegister(code);
      await loadRegisters();
    } catch (err) {
      setError(err.response?.data?.error || "Could not deactivate register");
    }
  };

  const handleActivateRegister = async (code) => {
    setError("");
    try {
      await activateRegister(code);
      await loadRegisters();
    } catch (err) {
      setError(err.response?.data?.error || "Could not reactivate register");
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    setError("");
    setCreating(true);
    try {
      await createUser({ ...form, role: "Cashier" });
      setForm(emptyForm);
      await loadUsers();
    } catch (err) {
      setError(err.response?.data?.error || "Could not create cashier account");
    } finally {
      setCreating(false);
    }
  };

  const handleDeactivate = async (user) => {
    setError("");
    try {
      await deactivateUser(user.id);
      await loadUsers();
    } catch (err) {
      setError(err.response?.data?.error || "Could not deactivate cashier");
    }
  };

  const handleActivate = async (user) => {
    setError("");
    try {
      await activateUser(user.id);
      await loadUsers();
    } catch (err) {
      setError(err.response?.data?.error || "Could not activate cashier");
    }
  };

  const handleDelete = async (user) => {
    setError("");
    if (!window.confirm(`Permanently delete ${user.username}'s account? This cannot be undone.`)) {
      return;
    }
    try {
      await deleteUser(user.id);
      if (selectedUser?.id === user.id) {
        setSelectedUser(null);
        setOrders([]);
      }
      await loadUsers();
    } catch (err) {
      setError(err.response?.data?.error || "Could not delete cashier");
    }
  };

  const handleViewSales = async (user) => {
    setError("");
    setSelectedUser(user);
    setOrdersLoading(true);
    try {
      const items = await fetchOrdersByCashier(user.id);
      setOrders(items);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load sales for this user");
      setOrders([]);
    } finally {
      setOrdersLoading(false);
    }
  };

  return (
    <div>
      <h2>Staff</h2>
      {error && <div className="error-banner">{error}</div>}

      <form className="inline-form" onSubmit={handleCreate}>
        <h3>Create a cashier account</h3>
        <input
          placeholder="Username"
          value={form.username}
          onChange={(e) => setForm({ ...form, username: e.target.value })}
          required
        />
        <input
          type="email"
          placeholder="Email"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          required
        />
        <input
          type="password"
          placeholder="Password"
          value={form.password}
          onChange={(e) => setForm({ ...form, password: e.target.value })}
          required
        />
        <input
          type="password"
          placeholder="Confirm password"
          value={form.confirmPassword}
          onChange={(e) => setForm({ ...form, confirmPassword: e.target.value })}
          required
        />
        <button type="submit" disabled={creating}>
          {creating ? "Creating..." : "Create cashier"}
        </button>
      </form>

      <h3>Staff accounts</h3>
      {loading ? (
        <p>Loading...</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Email</th>
              <th>Role</th>
              <th>Active</th>
              <th>Last login</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.username}</td>
                <td>{u.email}</td>
                <td>{u.role}</td>
                <td>{u.isActive ? "Yes" : "No"}</td>
                <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}</td>
                <td className="actions">
                  <button onClick={() => handleViewSales(u)}>View sales</button>
                  {u.role === "Cashier" && (
                    <>
                      {u.isActive ? (
                        <button onClick={() => handleDeactivate(u)} className="danger">
                          Deactivate
                        </button>
                      ) : (
                        <button onClick={() => handleActivate(u)}>Activate</button>
                      )}
                      <button onClick={() => handleDelete(u)} className="danger">
                        Delete
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <h3>Registers</h3>
      <form className="inline-form" onSubmit={handleCreateRegister}>
        <input
          placeholder="Code (e.g. REG-1)"
          value={registerForm.code}
          onChange={(e) => setRegisterForm({ ...registerForm, code: e.target.value })}
          required
        />
        <input
          placeholder="Name (optional)"
          value={registerForm.name}
          onChange={(e) => setRegisterForm({ ...registerForm, name: e.target.value })}
        />
        <button type="submit" disabled={creatingRegister}>
          {creatingRegister ? "Creating..." : "Add register"}
        </button>
      </form>

      {registersLoading ? (
        <p>Loading registers...</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Active</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {registers.map((r) => (
              <tr key={r.id}>
                <td>{r.code}</td>
                <td>{r.name || "-"}</td>
                <td>{r.isActive ? "Yes" : "No"}</td>
                <td className="actions">
                  {r.isActive ? (
                    <button onClick={() => handleDeactivateRegister(r.code)} className="danger">
                      Deactivate
                    </button>
                  ) : (
                    <button onClick={() => handleActivateRegister(r.code)}>Reactivate</button>
                  )}
                </td>
              </tr>
            ))}
            {registers.length === 0 && (
              <tr>
                <td colSpan={4} className="empty">
                  No registers yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}

      {selectedUser && (
        <>
          <h3>Sales by {selectedUser.username}</h3>
          {ordersLoading ? (
            <p>Loading sales...</p>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Order #</th>
                  <th>Date</th>
                  <th>Items</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((order) => (
                  <tr key={order.id}>
                    <td>{order.orderNumber.slice(0, 8)}</td>
                    <td>{new Date(order.orderDate).toLocaleString()}</td>
                    <td>{order.itemCount}</td>
                    <td>${order.totalAmount.toFixed(2)}</td>
                  </tr>
                ))}
                {orders.length === 0 && (
                  <tr>
                    <td colSpan={4} className="empty">
                      No sales recorded for this user yet.
                    </td>
                  </tr>
                )}
              </tbody>
              {orders.length > 0 && (
                <tfoot>
                  <tr>
                    <td colSpan={3}>
                      <strong>Total sales</strong>
                    </td>
                    <td>
                      <strong>${salesTotal.toFixed(2)}</strong>
                    </td>
                  </tr>
                </tfoot>
              )}
            </table>
          )}
        </>
      )}
    </div>
  );
}
