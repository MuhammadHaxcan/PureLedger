import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
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
import ProcessDC from './pages/DeliveryChallan/ProcessDC';
import Users from './pages/Admin/Users';
import EditProfile from './pages/Admin/EditProfile';
import CartageSettings from './pages/Admin/CartageSettings';
import AccessDenied from './pages/Account/AccessDenied';
import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
            <Route path="/" element={<Navigate to="/home/entry" replace />} />
            {/* Home Routes */}
            <Route path="/home/entry" element={<HomeEntry />} />
            <Route path="/home/voucher-entry" element={<VoucherEntry />} />
            <Route path="/home/base-vouchers" element={<BaseVouchers />} />
            <Route path="/home/process-base-voucher/:id" element={<ProcessBaseVoucher />} />
            <Route path="/home/modified-entries" element={<ModifiedEntries />} />
            <Route path="/home/voucher-modified-entries/:jvId" element={<VoucherModifiedEntries />} />
            <Route path="/home/recipes" element={<Recipes />} />
            {/* MetaData Routes */}
            <Route path="/metadata/projects" element={<Projects />} />
            <Route path="/metadata/categories" element={<Categories />} />
            <Route path="/metadata/accounts" element={<Accounts />} />
            <Route path="/metadata/locations" element={<Locations />} />
            <Route path="/metadata/bookers" element={<Bookers />} />
            {/* Reporting Routes */}
            <Route path="/reporting/single-doc" element={<SingleDoc />} />
            <Route path="/reporting/aging-report" element={<AgingReport />} />
            <Route path="/reporting/account-report" element={<AccountReport />} />
            <Route path="/reporting/master-report" element={<MasterReport />} />
            <Route path="/reporting/product-report" element={<ProductReport />} />
            <Route path="/reporting/missed-entries" element={<MissedEntriesReport />} />
            {/* Delivery Challan Routes */}
            <Route path="/delivery/process-dc" element={<ProcessDC />} />
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
                    <Route path="/admin/edit-profile" element={<EditProfile />} />
            {/* Account Routes */}
            <Route path="/account/access-denied" element={<AccessDenied />} />
            {/* 404 */}
            <Route path="*" element={<div className="text-center py-5"><h3>Page not found</h3></div>} />
          </Route>
        </Routes>
      </Router>
    </AuthProvider>
  );
}

export default App;
