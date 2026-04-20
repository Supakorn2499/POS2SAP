import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AppLayout } from '@/components/layout/AppLayout';
import DashboardPage from '@/pages/DashboardPage';
import MonitorPage from '@/pages/MonitorPage';
import MonitorDetailPage from '@/pages/MonitorDetailPage';
import ConfigPage from '@/pages/ConfigPage';
import LoginPage from '@/pages/LoginPage';
import { useAuth } from '@/contexts/AuthContext';

function App() {
  const { authenticated } = useAuth();

  const RequireAuth = ({ children }: { children: ReactNode }) =>
    authenticated ? <>{children}</> : <Navigate to="/login" replace />;

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={authenticated ? <Navigate to="/dashboard" replace /> : <LoginPage />} />
        <Route
          element={
            <RequireAuth>
              <AppLayout />
            </RequireAuth>
          }
        >
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/monitor" element={<MonitorPage />} />
          <Route path="/monitor/:id" element={<MonitorDetailPage />} />
          <Route path="/config" element={<ConfigPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
