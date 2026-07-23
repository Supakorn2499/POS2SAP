import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AppLayout } from '@/components/layout/AppLayout';
import DashboardPage from '@/pages/DashboardPage';
import MonitorPage from '@/pages/MonitorPage';
import MonitorDetailPage from '@/pages/MonitorDetailPage';
import ConfigPage from '@/pages/ConfigPage';
import AppLogsPage from '@/pages/AppLogsPage';
import ImportPage from '@/pages/ImportPage';
import GlMappingPage from '@/pages/GlMappingPage';
import ProductGroupMappingPage from '@/pages/ProductGroupMappingPage';
import ShopMappingPage from '@/pages/ShopMappingPage';
import UserGuidePage from '@/pages/UserGuidePage';
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
          <Route path="/import" element={<ImportPage />} />
          <Route path="/glmapping" element={<GlMappingPage />} />
          <Route path="/productgroupmapping" element={<ProductGroupMappingPage />} />
          <Route path="/shopmapping" element={<ShopMappingPage />} />
          <Route path="/app-logs" element={<AppLogsPage />} />
          <Route path="/guide" element={<UserGuidePage />} />
          <Route path="/config" element={<ConfigPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
