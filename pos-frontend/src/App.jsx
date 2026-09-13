import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import ProtectedRoute from "./components/ProtectedRoute";
import Layout from "./components/Layout";
import LoginPage from "./pages/LoginPage";
import CatalogPage from "./pages/CatalogPage";
import PosPage from "./pages/PosPage";
import CustomersPage from "./pages/CustomersPage";
import ShiftPage from "./pages/ShiftPage";
import StaffPage from "./pages/StaffPage";
import AuditLogPage from "./pages/AuditLogPage";
import "./App.css";

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            element={
              <ProtectedRoute>
                <Layout />
              </ProtectedRoute>
            }
          >
            <Route path="/" element={<CatalogPage />} />
            <Route path="/pos" element={<PosPage />} />
            <Route path="/customers" element={<CustomersPage />} />
            <Route path="/shift" element={<ShiftPage />} />
            <Route
              path="/staff"
              element={
                <ProtectedRoute requireAdmin>
                  <StaffPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/audit-log"
              element={
                <ProtectedRoute requireAdmin>
                  <AuditLogPage />
                </ProtectedRoute>
              }
            />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
