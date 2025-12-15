import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import { AuthProvider } from './contexts/AuthContext';
import { ToastProvider } from './contexts/ToastContext';
import { DataProvider } from './contexts/DataContext';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import Login from './pages/Login';
import HomeEntry from './pages/Home/Entry';
import VoucherEntry from './pages/Home/VoucherEntry';
import BaseVouchers from './pages/Home/BaseVouchers';
import ProcessBaseVoucher from './pages/Home/ProcessBaseVoucher';
import ModifiedEntries from './pages/Home/ModifiedEntries';
import VoucherModifiedEntries from './pages/Home/VoucherModifiedEntries';
import Recipes from './pages/Home/Recipes';
import Projects from './pages/MetaData/Projects';
import Categories from './pages/MetaData/Categories';
import Accounts from './pages/MetaData/Accounts';
import Locations from './pages/MetaData/Locations';
import Bookers from './pages/MetaData/Bookers';
import SingleDoc from './pages/Reporting/SingleDoc';
import AgingReport from './pages/Reporting/AgingReport';
import AccountReport from './pages/Reporting/AccountReport';
import MasterReport from './pages/Reporting/MasterReport';
import ProductReport from './pages/Reporting/ProductReport';
import MissedEntriesReport from './pages/Reporting/MissedEntriesReport';
import PrintOnlyVoucher from './pages/Reporting/PrintOnlyVoucher';
import ProcessDC from './pages/DeliveryChallan/ProcessDC';
import Users from './pages/Admin/Users';
import EditProfile from './pages/Admin/EditProfile';
import CartageSettings from './pages/Admin/CartageSettings';
import Signatures from './pages/Admin/Signatures';
import AccessDenied from './pages/Account/AccessDenied';
import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';

function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <DataProvider>
          <Router>
          <Routes>
          <Route path="/login" element={<Login />} />
          {/* Print-only route without layout */}
          <Route path="/reporting/print-voucher" element={<ProtectedRoute><PrintOnlyVoucher /></ProtectedRoute>} />
          <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
            <Route path="/" element={<Navigate to="/home/entry" replace />} />
            {/* Default route that checks access first */}
            <Route path="/home" element={<Navigate to="/home/entry" replace />} />
            {/* Home Routes */}
            <Route path="/home/entry" element={<ProtectedRoute><HomeEntry /></ProtectedRoute>} />
            <Route path="/home/voucher-entry" element={<ProtectedRoute><VoucherEntry /></ProtectedRoute>} />
            <Route path="/home/base-vouchers" element={<ProtectedRoute><BaseVouchers /></ProtectedRoute>} />
            <Route path="/home/process-base-voucher/:id" element={<ProtectedRoute><ProcessBaseVoucher /></ProtectedRoute>} />
            <Route path="/home/modified-entries" element={<ProtectedRoute><ModifiedEntries /></ProtectedRoute>} />
            <Route path="/home/voucher-modified-entries/:jvId" element={<ProtectedRoute><VoucherModifiedEntries /></ProtectedRoute>} />
            <Route path="/home/recipes" element={<ProtectedRoute><Recipes /></ProtectedRoute>} />
            {/* MetaData Routes */}
            <Route path="/metadata/projects" element={<ProtectedRoute><Projects /></ProtectedRoute>} />
            <Route path="/metadata/categories" element={<ProtectedRoute><Categories /></ProtectedRoute>} />
            <Route path="/metadata/accounts" element={<ProtectedRoute><Accounts /></ProtectedRoute>} />
            <Route path="/metadata/locations" element={<ProtectedRoute><Locations /></ProtectedRoute>} />
            <Route path="/metadata/bookers" element={<ProtectedRoute><Bookers /></ProtectedRoute>} />
            {/* Reporting Routes */}
            <Route path="/reporting/single-doc" element={<ProtectedRoute><SingleDoc /></ProtectedRoute>} />
            <Route path="/reporting/aging-report" element={<ProtectedRoute><AgingReport /></ProtectedRoute>} />
            <Route path="/reporting/account-report" element={<ProtectedRoute><AccountReport /></ProtectedRoute>} />
            <Route path="/reporting/master-report" element={<ProtectedRoute><MasterReport /></ProtectedRoute>} />
            <Route path="/reporting/product-report" element={<ProtectedRoute><ProductReport /></ProtectedRoute>} />
            <Route path="/reporting/missed-entries" element={<ProtectedRoute><MissedEntriesReport /></ProtectedRoute>} />
            {/* Delivery Challan Routes */}
            <Route path="/delivery/process-dc" element={<ProtectedRoute><ProcessDC /></ProtectedRoute>} />
            {/* Admin Routes */}
            <Route 
              path="/admin/users" 
              element={
                <ProtectedRoute requireAdmin={true}>
                  <Users />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/admin/cartage-settings" 
              element={
                <ProtectedRoute requireAdmin={true}>
                  <CartageSettings />
                </ProtectedRoute>
              } 
            />
            <Route 
              path="/admin/signatures" 
              element={
                <ProtectedRoute requireAdmin={true}>
                  <Signatures />
                </ProtectedRoute>
              } 
            />
            <Route path="/admin/edit-profile" element={<ProtectedRoute><EditProfile /></ProtectedRoute>} />
            {/* Account Routes */}
            <Route path="/account/access-denied" element={<AccessDenied />} />
            {/* 404 */}
            <Route path="*" element={<div className="text-center py-5"><h3>Page not found</h3></div>} />
          </Route>
        </Routes>
      </Router>
      </DataProvider>
    </AuthProvider>
    <ToastContainer hideProgressBar />
    </ToastProvider>
  );
}

export default App;
