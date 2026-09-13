import { Fragment, useEffect, useState } from "react";
import { fetchCurrentShift, openShift, closeShift, fetchXReport, fetchShiftOrders } from "../api/shifts";
import { fetchActiveRegisters } from "../api/registers";
import { fetchOrderDetail, processRefund } from "../api/orders";

const emptyRefundForm = { method: "Cash", reason: "" };

export default function ShiftPage() {
  const [shift, setShift] = useState(null);
  const [report, setReport] = useState(null);
  const [orders, setOrders] = useState([]);
  const [registers, setRegisters] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [openForm, setOpenForm] = useState({ registerCode: "", openingFloat: "100" });
  const [closeForm, setCloseForm] = useState({ closingCountedAmount: "" });

  // Refund panel state - which order is being refunded, its full line-item detail, and the
  // quantities the cashier has entered per line.
  const [refundingOrderId, setRefundingOrderId] = useState(null);
  const [refundOrder, setRefundOrder] = useState(null);
  const [refundQuantities, setRefundQuantities] = useState({});
  const [refundForm, setRefundForm] = useState(emptyRefundForm);
  const [refundSubmitting, setRefundSubmitting] = useState(false);

  const salesTotal = orders.reduce((sum, order) => sum + order.totalAmount, 0);

  const load = async () => {
    setError("");
    try {
      const [current, activeRegisters] = await Promise.all([fetchCurrentShift(), fetchActiveRegisters()]);
      setShift(current);
      setRegisters(activeRegisters);
      if (!openForm.registerCode && activeRegisters.length > 0) {
        setOpenForm((prev) => ({ ...prev, registerCode: activeRegisters[0].code }));
      }
      if (current) {
        const [r, shiftOrders] = await Promise.all([fetchXReport(current.id), fetchShiftOrders(current.id)]);
        setReport(r);
        setOrders(shiftOrders);
      } else {
        setReport(null);
        setOrders([]);
      }
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load shift");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleOpen = async (e) => {
    e.preventDefault();
    setError("");
    try {
      await openShift(openForm.registerCode, Number(openForm.openingFloat));
      await load();
    } catch (err) {
      setError(err.response?.data?.error || "Could not open shift");
    }
  };

  const handleClose = async (e) => {
    e.preventDefault();
    setError("");
    try {
      const finalReport = await closeShift(shift.id, Number(closeForm.closingCountedAmount));
      setReport(finalReport);
      setShift(null);
      setCloseForm({ closingCountedAmount: "" });
    } catch (err) {
      setError(err.response?.data?.error || "Could not close shift");
    }
  };

  const openRefundPanel = async (orderId) => {
    setError("");
    setRefundingOrderId(orderId);
    setRefundOrder(null);
    setRefundQuantities({});
    setRefundForm(emptyRefundForm);
    try {
      const detail = await fetchOrderDetail(orderId);
      setRefundOrder(detail);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to load order for refund");
      setRefundingOrderId(null);
    }
  };

  const closeRefundPanel = () => {
    setRefundingOrderId(null);
    setRefundOrder(null);
    setRefundQuantities({});
  };

  const handleSubmitRefund = async (e) => {
    e.preventDefault();
    setError("");

    const items = Object.entries(refundQuantities)
      .map(([orderItemId, qty]) => ({ orderItemId: Number(orderItemId), quantity: Number(qty) }))
      .filter((item) => item.quantity > 0);

    if (items.length === 0) {
      setError("Enter a quantity to refund for at least one item");
      return;
    }

    setRefundSubmitting(true);
    try {
      await processRefund(refundOrder.id, { items, method: refundForm.method, reason: refundForm.reason });
      closeRefundPanel();
      await load();
    } catch (err) {
      setError(err.response?.data?.error || "Refund failed");
    } finally {
      setRefundSubmitting(false);
    }
  };

  if (loading) return <p>Loading shift...</p>;

  return (
    <div>
      <h2>Shift</h2>
      {error && <div className="error-banner">{error}</div>}

      {!shift ? (
        <form className="inline-form" onSubmit={handleOpen}>
          <h3>Open a shift</h3>
          {registers.length === 0 ? (
            <p>No active registers - ask a SuperAdmin to add one on the Staff page.</p>
          ) : (
            <select
              value={openForm.registerCode}
              onChange={(e) => setOpenForm({ ...openForm, registerCode: e.target.value })}
              required
            >
              {registers.map((r) => (
                <option key={r.id} value={r.code}>
                  {r.code}
                  {r.name ? ` - ${r.name}` : ""}
                </option>
              ))}
            </select>
          )}
          <input
            type="number"
            step="0.01"
            placeholder="Opening float"
            value={openForm.openingFloat}
            onChange={(e) => setOpenForm({ ...openForm, openingFloat: e.target.value })}
            required
          />
          <button type="submit" disabled={registers.length === 0}>
            Open shift
          </button>
        </form>
      ) : (
        <>
          <div className="shift-summary">
            <div>
              <strong>Register:</strong> {shift.registerCode}
            </div>
            <div>
              <strong>Opened:</strong> {new Date(shift.openedAt).toLocaleString()}
            </div>
            <div>
              <strong>Opening float:</strong> ${shift.openingFloat.toFixed(2)}
            </div>
          </div>

          <h3>Sales this shift</h3>
          <table className="data-table">
            <thead>
              <tr>
                <th>Order #</th>
                <th>Time</th>
                <th>Items</th>
                <th>Total</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <Fragment key={order.id}>
                  <tr>
                    <td>{order.orderNumber.slice(0, 8)}</td>
                    <td>{new Date(order.orderDate).toLocaleTimeString()}</td>
                    <td>{order.itemCount}</td>
                    <td>${order.totalAmount.toFixed(2)}</td>
                    <td className="actions">
                      {refundingOrderId === order.id ? (
                        <button onClick={closeRefundPanel}>Cancel</button>
                      ) : (
                        <button onClick={() => openRefundPanel(order.id)}>Refund</button>
                      )}
                    </td>
                  </tr>
                  {refundingOrderId === order.id && (
                    <tr>
                      <td colSpan={5}>
                        {!refundOrder ? (
                          <p>Loading order...</p>
                        ) : (
                          <form className="inline-form" onSubmit={handleSubmitRefund}>
                            <h3>Refund order {refundOrder.orderNumber.slice(0, 8)}</h3>
                            {refundOrder.items.map((item) => {
                              const remaining = item.quantity - item.quantityRefunded;
                              return (
                                <label key={item.id}>
                                  {item.productName} (${item.unitPrice.toFixed(2)} each, {remaining} refundable)
                                  <input
                                    type="number"
                                    min="0"
                                    max={remaining}
                                    disabled={remaining === 0}
                                    value={refundQuantities[item.id] || ""}
                                    onChange={(e) =>
                                      setRefundQuantities({ ...refundQuantities, [item.id]: e.target.value })
                                    }
                                  />
                                </label>
                              );
                            })}
                            <label>
                              Refund method
                              <select
                                value={refundForm.method}
                                onChange={(e) => setRefundForm({ ...refundForm, method: e.target.value })}
                              >
                                <option value="Cash">Cash</option>
                                <option value="Card">Card</option>
                                <option value="GiftCard">Gift Card</option>
                              </select>
                            </label>
                            <label>
                              Reason (optional)
                              <input
                                value={refundForm.reason}
                                onChange={(e) => setRefundForm({ ...refundForm, reason: e.target.value })}
                              />
                            </label>
                            <button type="submit" className="danger" disabled={refundSubmitting}>
                              {refundSubmitting ? "Processing..." : "Process refund"}
                            </button>
                          </form>
                        )}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {orders.length === 0 && (
                <tr>
                  <td colSpan={5} className="empty">
                    No sales yet this shift.
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
                  <td colSpan={2}>
                    <strong>${salesTotal.toFixed(2)}</strong>
                  </td>
                </tr>
              </tfoot>
            )}
          </table>

          {report && (
            <table className="data-table">
              <tbody>
                <tr>
                  <td>Expected cash</td>
                  <td>${report.expectedCash.toFixed(2)}</td>
                </tr>
                {report.cashRefundsTotal > 0 && (
                  <tr>
                    <td>Cash refunds</td>
                    <td>-${report.cashRefundsTotal.toFixed(2)}</td>
                  </tr>
                )}
                {report.isFinal && (
                  <>
                    <tr>
                      <td>Counted cash</td>
                      <td>${report.closingCountedAmount?.toFixed(2)}</td>
                    </tr>
                    <tr>
                      <td>Variance</td>
                      <td>${report.variance?.toFixed(2)}</td>
                    </tr>
                  </>
                )}
              </tbody>
            </table>
          )}

          <form className="inline-form" onSubmit={handleClose}>
            <h3>Close shift</h3>
            <input
              type="number"
              step="0.01"
              placeholder="Cash counted"
              value={closeForm.closingCountedAmount}
              onChange={(e) => setCloseForm({ ...closeForm, closingCountedAmount: e.target.value })}
              required
            />
            <button type="submit" className="danger">
              Close shift
            </button>
          </form>
        </>
      )}
    </div>
  );
}
