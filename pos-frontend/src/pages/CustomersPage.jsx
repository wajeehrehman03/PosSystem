import { useState } from "react";
import { fetchCustomerByPhone, registerCustomer, adjustLoyaltyPoints, fetchLoyaltyHistory } from "../api/customers";
import { useAuth } from "../context/AuthContext";

const emptyRegisterForm = { phone: "", name: "" };
const emptyAdjustForm = { points: "", reason: "" };

export default function CustomersPage() {
  const { isSuperAdmin } = useAuth();
  const [searchPhone, setSearchPhone] = useState("");
  const [customer, setCustomer] = useState(null);
  const [history, setHistory] = useState([]);
  const [searched, setSearched] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [registerForm, setRegisterForm] = useState(emptyRegisterForm);
  const [adjustForm, setAdjustForm] = useState(emptyAdjustForm);

  const loadCustomer = async (phone) => {
    setError("");
    setMessage("");
    try {
      const found = await fetchCustomerByPhone(phone);
      setCustomer(found);
      setSearched(true);
      setHistory(found ? await fetchLoyaltyHistory(phone) : []);
    } catch (err) {
      setError(err.response?.data?.error || "Lookup failed");
    }
  };

  const handleSearch = async (e) => {
    e.preventDefault();
    const phone = searchPhone.trim();
    if (!phone) return;
    await loadCustomer(phone);
  };

  const handleRegister = async (e) => {
    e.preventDefault();
    setError("");
    setMessage("");
    try {
      const created = await registerCustomer(registerForm.phone.trim(), registerForm.name.trim());
      setMessage(`Registered ${created.name}.`);
      setRegisterForm(emptyRegisterForm);
      setSearchPhone(created.phone);
      await loadCustomer(created.phone);
    } catch (err) {
      setError(err.response?.data?.error || "Registration failed");
    }
  };

  const handleAdjust = async (e) => {
    e.preventDefault();
    setError("");
    setMessage("");
    const points = Number(adjustForm.points);
    if (!points) {
      setError("Points must be a non-zero number");
      return;
    }
    try {
      const updated = await adjustLoyaltyPoints(customer.phone, points, adjustForm.reason.trim());
      setCustomer(updated);
      setHistory(await fetchLoyaltyHistory(customer.phone));
      setAdjustForm(emptyAdjustForm);
      setMessage(`${points > 0 ? "Credited" : "Debited"} ${Math.abs(points)} points.`);
    } catch (err) {
      setError(err.response?.data?.error || "Adjustment failed");
    }
  };

  return (
    <div>
      <h2>Customers</h2>
      {error && <div className="error-banner">{error}</div>}
      {message && <div className="hint">{message}</div>}

      <form className="inline-form" onSubmit={handleSearch}>
        <input
          placeholder="Search by phone number"
          value={searchPhone}
          onChange={(e) => setSearchPhone(e.target.value)}
        />
        <button type="submit">Look up</button>
      </form>

      {searched && !customer && (
        <div className="warning-banner">
          No customer found with phone "{searchPhone.trim()}". Register them below.
        </div>
      )}

      {customer && (
        <div className="receipt-box">
          <h3>{customer.name}</h3>
          <table className="data-table">
            <tbody>
              <tr>
                <td>Phone</td>
                <td>{customer.phone}</td>
              </tr>
              <tr>
                <td>Loyalty points</td>
                <td>{customer.loyaltyPoints}</td>
              </tr>
              <tr>
                <td>Tier</td>
                <td>{customer.tier}</td>
              </tr>
            </tbody>
          </table>

          {isSuperAdmin && (
            <form className="inline-form" onSubmit={handleAdjust}>
              <input
                type="number"
                placeholder="Points (+credit / -debit)"
                value={adjustForm.points}
                onChange={(e) => setAdjustForm({ ...adjustForm, points: e.target.value })}
                required
              />
              <input
                placeholder="Reason (optional)"
                value={adjustForm.reason}
                onChange={(e) => setAdjustForm({ ...adjustForm, reason: e.target.value })}
              />
              <button type="submit">Adjust points</button>
            </form>
          )}

          {history.length > 0 && (
            <details open>
              <summary>Loyalty history</summary>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Type</th>
                    <th>Points</th>
                    <th>Reason</th>
                  </tr>
                </thead>
                <tbody>
                  {history.map((t) => (
                    <tr key={t.id}>
                      <td>{new Date(t.createdAt).toLocaleString()}</td>
                      <td>{t.type}</td>
                      <td>{t.points}</td>
                      <td>{t.reason || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </details>
          )}
        </div>
      )}

      <h3>Register a new customer</h3>
      <form className="inline-form" onSubmit={handleRegister}>
        <input
          placeholder="Phone"
          value={registerForm.phone}
          onChange={(e) => setRegisterForm({ ...registerForm, phone: e.target.value })}
          required
        />
        <input
          placeholder="Name"
          value={registerForm.name}
          onChange={(e) => setRegisterForm({ ...registerForm, name: e.target.value })}
          required
        />
        <button type="submit">Add customer</button>
      </form>
    </div>
  );
}
