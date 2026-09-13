import { useEffect, useState } from "react";
import { fetchAuditLog } from "../api/auditLog";

export default function AuditLogPage() {
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      setError("");
      try {
        const items = await fetchAuditLog();
        setEntries(items);
      } catch (err) {
        setError(err.response?.data?.error || "Failed to load audit log");
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  return (
    <div>
      <h2>Audit Log</h2>
      <p className="hint">Recent administrative actions - product changes, price changes, and new user accounts.</p>
      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p>Loading...</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>User</th>
              <th>Action</th>
              <th>Details</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td>{new Date(entry.createdAt).toLocaleString()}</td>
                <td>{entry.username}</td>
                <td>{entry.action}</td>
                <td>{entry.details}</td>
              </tr>
            ))}
            {entries.length === 0 && (
              <tr>
                <td colSpan={4} className="empty">
                  No activity recorded yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
