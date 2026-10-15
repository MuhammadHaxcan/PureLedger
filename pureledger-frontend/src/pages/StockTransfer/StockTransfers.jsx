import { todayString } from '../../utils/businessDate';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Col,
  Form,
  InputGroup,
  Modal,
  Row,
  Spinner,
  Tab,
  Table,
  Tabs,
} from 'react-bootstrap';
import { useSearchParams } from 'react-router-dom';
import SearchableSelect from '../../components/SearchableSelect';
import apiClient from '../../services/api';
import { transferCarryingValue } from '../../utils/stockTransferCost';
import { ACCOUNT_GROUP } from '../../constants/accountGroups';
import { usePageTitle } from '../../hooks/usePageTitle';
import { useAuth } from '../../contexts/AuthContext';
import {
  rememberLastSelectedProject,
  resolveLastSelectedProject,
} from '../../utils/lastSelectedProject';
import './StockTransfers.css';
import { calculateAllocations } from '../../utils/chargeAllocation';

const API = '/StockTransferApi';
const PAGE_SIZE = 2500;
const ACTIVE_STATUSES = ['Pending', 'PartiallyReceived'];
const FINAL_STATUSES = ['Received', 'Cancelled', 'ClosedShort'];
const today = () => todayString();
const money = (value) => Number(value || 0).toLocaleString(undefined, {
  minimumFractionDigits: 2,
  maximumFractionDigits: 4,
});
const round4 = (value) => Math.round((Number(value) + Number.EPSILON) * 10000) / 10000;
const unwrap = (response) => response?.data?.data ?? response?.data ?? {};
const blankItem = () => ({
  categoryId: '',
  productAccountId: '',
  quantity: '',
  unitCost: '',
  unitCostOverridden: false,
  suggestedUnitCost: null,
  availableQuantity: null,
  inventoryValue: null,
  accounts: [],
});
const blankCharge = () => ({
  payeeAccountId: '',
  amount: '',
  description: '',
  targetType: 'Products',
  allocationMethod: 'Equal',
  targetAccountId: '',
  allocations: {},
});

const statusVariant = (status) => ({
  Pending: 'warning',
  PartiallyReceived: 'info',
  Received: 'success',
  Cancelled: 'secondary',
  ClosedShort: 'dark',
}[status] || 'secondary');

const normalizeItem = (item) => {
  const quantity = Number(item.quantity ?? item.dispatchedQuantity ?? 0);
  const lineState = item.lineState
    || (Number(item.remainingQuantity ?? quantity) > 0 ? 'Pending' : 'Received');
  const unitCost = Number(item.unitCost ?? item.rate ?? 0);
  const baseValue = Number(item.baseValue ?? item.dispatchedValue ?? quantity * unitCost);
  return {
    ...item,
    productAccountId: Number(item.productAccountId ?? item.accountId ?? item.id),
    productName: item.productName ?? item.accountName ?? `Product ${item.productAccountId ?? item.accountId ?? ''}`,
    categoryId: item.categoryId ?? '',
    categoryName: item.categoryName ?? '',
    quantity,
    dispatchedQuantity: quantity,
    unitCost,
    baseValue,
    lineState,
    receivedQuantity: lineState === 'Received' ? quantity : 0,
    cancelledQuantity: lineState === 'Cancelled' ? quantity : 0,
    remainingQuantity: lineState === 'Pending' ? quantity : 0,
  };
};

const normalizeTransfer = (transfer) => {
  const items = (transfer.items || []).map(normalizeItem);
  const status = transfer.status === 'InTransit' ? 'Pending' : transfer.status;
  const dispatchedQuantity = items.reduce((sum, item) => sum + item.quantity, 0);
  const receivedQuantity = items.reduce((sum, item) => sum + item.receivedQuantity, 0);
  const remainingQuantity = items.reduce((sum, item) => sum + item.remainingQuantity, 0);
  const remainingValue = items.reduce((sum, item) => sum + (item.lineState === 'Pending' ? item.baseValue : 0), 0);
  const dispatchJvId = Number(transfer.dispatchJvId ?? transfer.id);
  return {
    ...transfer,
    id: dispatchJvId,
    dispatchJvId,
    transferNumber: transfer.transferNumber || `JV ${dispatchJvId}`,
    rowVersion: transfer.rowVersion ?? transfer.version,
    status,
    items,
    totalDispatchedQuantity: Number(transfer.totalDispatchedQuantity ?? dispatchedQuantity),
    totalReceivedQuantity: Number(transfer.totalReceivedQuantity ?? receivedQuantity),
    totalRemainingQuantity: Number(transfer.totalRemainingQuantity ?? remainingQuantity),
    remainingValue: Number(transfer.remainingValue ?? remainingValue),
  };
};

export default function StockTransfers() {
  usePageTitle('Stock Transfers');
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState('new');
  const itemsTableBodyRef = useRef(null);
  const focusNewItemRef = useRef(false);
  const [init, setInit] = useState({ projects: [], locations: [] });
  const [categories, setCategories] = useState([]);
  const [filterCategories, setFilterCategories] = useState([]);
  const [transitCategories, setTransitCategories] = useState([]);
  const [normalAccounts, setNormalAccounts] = useState([]);
  const [normalAccountsProjectId, setNormalAccountsProjectId] = useState(null);
  const [transitSettings, setTransitSettings] = useState(null);
  const [transitSettingForm, setTransitSettingForm] = useState({
    inTransitCategoryId: '',
    inTransitAccountId: '',
  });
  const [transitAccounts, setTransitAccounts] = useState([]);
  const [form, setForm] = useState({
    projectId: '',
    sourceLocationId: '',
    destinationLocationId: '',
    transferDate: today(),
    notes: '',
    items: [blankItem()],
  });
  const [editing, setEditing] = useState(null);
  const [filters, setFilters] = useState({
    projectId: '',
    categoryId: '',
    locationId: '',
    dispatchFromDate: '',
    dispatchToDate: '',
  });
  const [resultSearch, setResultSearch] = useState('');
  const [transfers, setTransfers] = useState([]);
  const [transferTotalCount, setTransferTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [receiveTransfer, setReceiveTransfer] = useState(null);
  const [receiptDate, setReceiptDate] = useState(today());
  const [selectedReceiptProducts, setSelectedReceiptProducts] = useState({});
  const [charges, setCharges] = useState([]);
  const [details, setDetails] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteReason, setDeleteReason] = useState('');
  const loadRequestIdRef = useRef(0);
  const initializedProjectRef = useRef(false);

  const showApiError = useCallback((err) => {
    setError(err?.response?.data?.message || err?.message || 'Request failed.');
  }, []);
  const clearMessages = () => {
    setError('');
    setSuccess('');
  };

  useEffect(() => {
    apiClient.get(`${API}/init`)
      .then((response) => setInit(unwrap(response)))
      .catch(showApiError);
  }, [showApiError]);

  const loadTransfers = useCallback(async () => {
    const requestId = ++loadRequestIdRef.current;
    setError('');
    if (!filters.projectId) {
      setTransfers([]);
      setTransferTotalCount(0);
      setLoading(false);
      return;
    }
    setLoading(true);
    setTransfers([]);
    setTransferTotalCount(0);
    try {
      const params = Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== ''));
      if (params.dispatchFromDate) params.fromDate = params.dispatchFromDate;
      if (params.dispatchToDate) params.toDate = params.dispatchToDate;
      delete params.dispatchFromDate;
      delete params.dispatchToDate;
      params.statusGroup = activeTab === 'history' ? 'Final' : 'Open';

      const loaded = [];
      let page = 0;
      let hasMoreData = true;
      while (hasMoreData) {
        const response = await apiClient.get(API, {
          params: { ...params, page, pageSize: PAGE_SIZE },
        });
        if (loadRequestIdRef.current !== requestId) return;
        const payload = unwrap(response);
        const rows = (payload.items || payload || []).map(normalizeTransfer);
        loaded.push(...rows);
        setTransfers([...loaded]);
        setTransferTotalCount(Number(payload.totalCount ?? loaded.length));
        hasMoreData = Boolean(payload.hasMoreData);
        page += 1;
      }
    } catch (err) {
      if (loadRequestIdRef.current === requestId) showApiError(err);
    } finally {
      if (loadRequestIdRef.current === requestId) setLoading(false);
    }
  }, [activeTab, filters, showApiError]);

  useEffect(() => {
    if (activeTab !== 'new') {
      loadTransfers();
    } else {
      loadRequestIdRef.current += 1;
      setLoading(false);
    }
  }, [activeTab]); // eslint-disable-line react-hooks/exhaustive-deps

  const showDetails = useCallback(async (id) => {
    clearMessages();
    try {
      const response = await apiClient.get(`${API}/${id}`);
      const payload = unwrap(response);
      const summary = normalizeTransfer(payload.summary || payload);
      setDetails({ ...payload, summary });
    } catch (err) {
      showApiError(err);
    }
  }, [showApiError]);

  useEffect(() => {
    const transferId = Number(searchParams.get('transferId'));
    if (transferId > 0) {
      setActiveTab('transit');
      showDetails(transferId);
      setSearchParams({}, { replace: true });
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const loadCategories = async (projectId) => {
    setCategories([]);
    setTransitCategories([]);
    if (!projectId) return;
    const [productResponse, transitResponse] = await Promise.all([
      apiClient.get(`${API}/categories`, {
        params: { projectId, accountGroupId: ACCOUNT_GROUP.PRODUCT },
      }),
      apiClient.get(`${API}/categories`, {
        params: { projectId, accountGroupId: ACCOUNT_GROUP.ACCOUNT },
      }),
    ]);
    const productResult = unwrap(productResponse);
    const transitResult = unwrap(transitResponse);
    setCategories(Array.isArray(productResult) ? productResult : productResult.items || []);
    setTransitCategories(Array.isArray(transitResult) ? transitResult : transitResult.items || []);
  };

  useEffect(() => {
    let cancelled = false;
    setFilterCategories([]);
    if (filters.projectId) {
      apiClient.get(`${API}/categories`, {
        params: { projectId: filters.projectId, accountGroupId: ACCOUNT_GROUP.PRODUCT },
      }).then((response) => {
        if (cancelled) return;
        const result = unwrap(response);
        setFilterCategories(Array.isArray(result) ? result : result.items || []);
      }).catch((err) => {
        if (!cancelled) showApiError(err);
      });
    }
    return () => { cancelled = true; };
  }, [filters.projectId, showApiError]);

  const loadAccountsForCategory = async (categoryId) => {
    if (!categoryId) return [];
    const response = await apiClient.get(`${API}/accounts`, {
      params: { categoryId, accountGroupId: ACCOUNT_GROUP.PRODUCT },
    });
    const result = unwrap(response);
    return Array.isArray(result) ? result : result.items || [];
  };

  const loadNormalAccounts = async (projectId) => {
    if (!projectId || String(normalAccountsProjectId) === String(projectId)) return;
    const responses = await Promise.all([
      apiClient.get(`${API}/project-accounts`, {
        params: { projectId, accountGroupId: ACCOUNT_GROUP.ACCOUNT },
      }),
      apiClient.get(`${API}/project-accounts`, {
        params: { projectId, accountGroupId: ACCOUNT_GROUP.EXPENSE },
      }),
    ]);
    const rows = responses.flatMap((response) => {
      const result = unwrap(response);
      return Array.isArray(result) ? result : result.items || [];
    });
    const uniqueRows = Array.from(new Map(rows.map((account) => [account.id, account])).values());
    setNormalAccounts(uniqueRows);
    setNormalAccountsProjectId(projectId);
  };

  const onProjectChange = async (projectId, rememberProject = true) => {
    clearMessages();
    setEditing(null);
    setTransitSettings(null);
    setTransitSettingForm({ inTransitCategoryId: '', inTransitAccountId: '' });
    setTransitAccounts([]);
    setForm((previous) => ({ ...previous, projectId, items: [blankItem()] }));
    setFilters((previous) => ({ ...previous, projectId: projectId || '', categoryId: '' }));
    if (rememberProject) rememberLastSelectedProject(projectId);
    try {
      const [, settingsResponse] = await Promise.all([
        loadCategories(projectId),
        projectId
          ? apiClient.get(`${API}/settings`, { params: { projectId } })
          : Promise.resolve(null),
      ]);
      if (settingsResponse) {
        const settings = unwrap(settingsResponse);
        setTransitSettings(settings);
        setTransitSettingForm({
          inTransitCategoryId: settings.inTransitCategoryId || '',
          inTransitAccountId: settings.inTransitAccountId || '',
        });
        if (settings.inTransitCategoryId) {
          const response = await apiClient.get(`${API}/accounts`, {
            params: { categoryId: settings.inTransitCategoryId, accountGroupId: ACCOUNT_GROUP.ACCOUNT },
          });
          const result = unwrap(response);
          setTransitAccounts(Array.isArray(result) ? result : result.items || []);
        }
      }
    } catch (err) {
      showApiError(err);
    }
  };

  useEffect(() => {
    if (initializedProjectRef.current || editing || form.projectId || !(init.projects || []).length) return;

    initializedProjectRef.current = true;
    const initialProjectId = resolveLastSelectedProject(init.projects);
    void onProjectChange(initialProjectId, false);
    // Project initialization intentionally runs once after the authorized project list loads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [init.projects]);

  const changeTransitCategory = async (categoryId) => {
    setTransitSettingForm({ inTransitCategoryId: categoryId, inTransitAccountId: '' });
    setTransitAccounts([]);
    if (!categoryId) return;
    try {
      const response = await apiClient.get(`${API}/accounts`, {
        params: { categoryId, accountGroupId: ACCOUNT_GROUP.ACCOUNT },
      });
      const result = unwrap(response);
      setTransitAccounts(Array.isArray(result) ? result : result.items || []);
    } catch (err) {
      showApiError(err);
    }
  };

  const saveTransitSettings = async () => {
    if (!form.projectId || !transitSettingForm.inTransitCategoryId
      || !transitSettingForm.inTransitAccountId) {
      setError('Select both the Stock In Transit category and account.');
      return;
    }
    setLoading(true);
    clearMessages();
    try {
      const response = await apiClient.put(`${API}/settings`, {
        projectId: Number(form.projectId),
        inTransitCategoryId: Number(transitSettingForm.inTransitCategoryId),
        inTransitAccountId: Number(transitSettingForm.inTransitAccountId),
      });
      const settings = unwrap(response);
      setTransitSettings(settings);
      setSuccess(`Stock In Transit will post to ${settings.inTransitAccountName}.`);
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const loadRowAccounts = async (index, categoryId) => {
    try {
      const accounts = await loadAccountsForCategory(categoryId);
      setForm((previous) => {
        const items = [...previous.items];
        items[index] = {
          ...items[index],
          categoryId,
          productAccountId: '',
          accounts,
          availableQuantity: null,
          inventoryValue: null,
          suggestedUnitCost: null,
          unitCost: '',
          unitCostOverridden: false,
        };
        return { ...previous, items };
      });
    } catch (err) {
      showApiError(err);
    }
  };

  const getAvailability = async (draft, item) => {
    const excludeDispatchJvId = editing?.id;
    const response = await apiClient.get(`${API}/availability`, {
      params: {
        projectId: draft.projectId,
        sourceLocationId: draft.sourceLocationId,
        productAccountId: item.productAccountId,
        asOfDate: draft.transferDate,
        excludeDispatchJvId,
      },
    });
    return unwrap(response);
  };

  const loadAvailability = async (index, productAccountId) => {
    const draft = {
      ...form,
      items: form.items.map((item, itemIndex) =>
        itemIndex === index
          ? { ...item, productAccountId, availableQuantity: null, inventoryValue: null, suggestedUnitCost: null, unitCost: '', unitCostOverridden: false }
          : item),
    };
    setForm(draft);
    if (!draft.projectId || !draft.sourceLocationId || !productAccountId) return;
    try {
      const availability = await getAvailability(draft, draft.items[index]);
      setForm((previous) => {
        if (previous.projectId !== draft.projectId || previous.sourceLocationId !== draft.sourceLocationId
          || previous.transferDate !== draft.transferDate || previous.items[index]?.productAccountId !== productAccountId) return previous;
        const items = [...previous.items];
        items[index] = {
          ...items[index],
          productAccountId,
          availableQuantity: availability.availableQuantity,
          inventoryValue: availability.inventoryValue ?? null,
          suggestedUnitCost: availability.suggestedUnitCost,
          unitCost: availability.suggestedUnitCost ?? '',
          unitCostOverridden: false,
        };
        return { ...previous, items };
      });
    } catch (err) {
      showApiError(err);
    }
  };

  const refreshAllAvailability = async (draft) => {
    if (!draft.projectId || !draft.sourceLocationId) return;
    const results = await Promise.all(draft.items.map(async (item) =>
      item.productAccountId ? getAvailability(draft, item) : null));
    setForm((previous) => previous.projectId !== draft.projectId || previous.sourceLocationId !== draft.sourceLocationId
      || previous.transferDate !== draft.transferDate ? previous : ({
      ...previous,
      items: previous.items.map((item, index) => {
        const availability = results[index];
        if (!availability || item.productAccountId !== draft.items[index]?.productAccountId) return item;
        return {
          ...item,
          availableQuantity: availability.availableQuantity,
          inventoryValue: availability.inventoryValue ?? null,
          suggestedUnitCost: availability.suggestedUnitCost,
          unitCost: item.unitCostOverridden ? item.unitCost : availability.suggestedUnitCost ?? '',
        };
      }),
    }));
  };

  const updateHeader = async (field, value) => {
    const draft = { ...form, [field]: value };
    if (field === 'sourceLocationId' || field === 'transferDate') {
      draft.items = draft.items.map(item => ({ ...item, availableQuantity: null, inventoryValue: null,
        unitCost: item.unitCostOverridden ? item.unitCost : '', suggestedUnitCost: null }));
    }
    setForm(draft);
    if (field === 'sourceLocationId' || field === 'transferDate') {
      try {
        await refreshAllAvailability(draft);
      } catch (err) {
        showApiError(err);
      }
    }
  };

  const addItem = useCallback(() => {
    focusNewItemRef.current = true;
    setForm((previous) => ({
      ...previous,
      items: [...previous.items, blankItem()],
    }));
  }, []);

  useEffect(() => {
    if (!focusNewItemRef.current) return;
    focusNewItemRef.current = false;
    const rows = itemsTableBodyRef.current?.querySelectorAll('tr');
    const lastRow = rows?.[rows.length - 1];
    if (!lastRow) return;
    lastRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    lastRow.querySelector('input.select-search')?.focus();
  }, [form.items.length]);

  useEffect(() => {
    if (activeTab !== 'new') return undefined;
    const handleKeyDown = (event) => {
      if (event.altKey && event.key.toLowerCase() === 'n' && !loading) {
        event.preventDefault();
        addItem();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [activeTab, loading, addItem]);

  const updateItem = (index, field, value) => setForm((previous) => ({
    ...previous,
    items: previous.items.map((item, itemIndex) =>
      itemIndex === index
        ? {
          ...item,
          [field]: value,
          ...(field === 'unitCost' ? { unitCostOverridden: true } : {}),
        }
        : item),
  }));

  const validateDispatch = () => {
    const rows = form.items.filter((item) => item.productAccountId);
    if (!form.projectId || !form.sourceLocationId || !form.destinationLocationId || !rows.length) {
      return 'Select the project, both warehouses, and at least one product.';
    }
    if (String(form.sourceLocationId) === String(form.destinationLocationId)) {
      return 'Source and destination warehouses must differ.';
    }
    if (editing && new Date(form.transferDate).getFullYear()
      !== new Date(editing.transferDate || editing.dispatchDate).getFullYear()) {
      return 'A pending transfer date may only change within its original calendar year.';
    }
    if (new Set(rows.map((item) => Number(item.productAccountId))).size !== rows.length) {
      return 'Each product may only appear once in a transfer.';
    }
    if (rows.some((item) => !Number.isInteger(Number(item.quantity)) || Number(item.quantity) <= 0)) {
      return 'Every product requires a positive whole quantity.';
    }
    if (rows.some((item) => item.unitCost === '' || Number(item.unitCost) <= 0)) {
      return 'Every product requires a positive transfer cost.';
    }
    return null;
  };

  const submitTransfer = async (event) => {
    event.preventDefault();
    clearMessages();
    const validationError = validateDispatch();
    if (validationError) {
      setError(validationError);
      return;
    }
    const request = {
      ...(editing ? { expectedRowVersion: editing.rowVersion } : {}),
      projectId: Number(form.projectId),
      sourceLocationId: Number(form.sourceLocationId),
      destinationLocationId: Number(form.destinationLocationId),
      transferDate: form.transferDate,
      notes: form.notes || null,
      items: form.items.filter((item) => item.productAccountId).map((item) => ({
        productAccountId: Number(item.productAccountId),
        quantity: Number(item.quantity),
        unitCost: item.unitCostOverridden ? Number(item.unitCost) : null,
      })),
    };
    setLoading(true);
    try {
      const response = editing
        ? await apiClient.put(`${API}/${editing.id}`, request)
        : await apiClient.post(`${API}/dispatch`, request);
      const result = unwrap(response);
      setSuccess(`${result.transferNumber || editing?.transferNumber || 'Stock transfer'} ${editing ? 'updated' : 'dispatched'} successfully.`);
      rememberLastSelectedProject(form.projectId);
      setEditing(null);
      setForm({
        projectId: form.projectId,
        sourceLocationId: form.sourceLocationId,
        destinationLocationId: '',
        transferDate: today(),
        notes: '',
        items: [blankItem()],
      });
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const beginEdit = async (transfer) => {
    if (transfer.status !== 'Pending') return;
    setLoading(true);
    clearMessages();
    try {
      const response = await apiClient.get(`${API}/${transfer.id}`);
      const payload = unwrap(response);
      const summary = normalizeTransfer(payload.summary || payload);
      const [, settingsResponse] = await Promise.all([
        loadCategories(summary.projectId),
        apiClient.get(`${API}/settings`, { params: { projectId: summary.projectId } }),
      ]);
      const settings = unwrap(settingsResponse);
      setTransitSettings(settings);
      setTransitSettingForm({
        inTransitCategoryId: settings.inTransitCategoryId || '',
        inTransitAccountId: settings.inTransitAccountId || '',
      });
      if (settings.inTransitCategoryId) {
        const response = await apiClient.get(`${API}/accounts`, {
            params: { categoryId: settings.inTransitCategoryId, accountGroupId: ACCOUNT_GROUP.ACCOUNT },
        });
        const result = unwrap(response);
        setTransitAccounts(Array.isArray(result) ? result : result.items || []);
      }
      const items = await Promise.all(summary.items.map(async (item) => {
        const [accounts, availabilityResponse] = await Promise.all([
          loadAccountsForCategory(item.categoryId),
          apiClient.get(`${API}/availability`, {
            params: {
              projectId: summary.projectId,
              sourceLocationId: summary.sourceLocationId,
              productAccountId: item.productAccountId,
              asOfDate: summary.transferDate || summary.dispatchDate,
              excludeDispatchJvId: summary.id,
            },
          }),
        ]);
        const availability = unwrap(availabilityResponse);
        return {
          categoryId: item.categoryId,
          productAccountId: item.productAccountId,
          quantity: item.quantity,
          unitCost: item.unitCost,
          unitCostOverridden: Number(item.unitCost) !== Number(availability.suggestedUnitCost),
          suggestedUnitCost: availability.suggestedUnitCost ?? null,
          availableQuantity: Number(availability.availableQuantity || 0),
          inventoryValue: availability.inventoryValue ?? null,
          accounts,
        };
      }));
      const draft = {
        projectId: String(summary.projectId),
        sourceLocationId: String(summary.sourceLocationId),
        destinationLocationId: String(summary.destinationLocationId),
        transferDate: summary.transferDate || summary.dispatchDate,
        notes: payload.notes || summary.notes || '',
        items,
      };
      setEditing(summary);
      setForm(draft);
      setDetails(null);
      setActiveTab('new');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const filteredByTab = useMemo(() => {
    const term = resultSearch.trim().toLowerCase();
    return transfers.filter((transfer) => {
      const belongsToTab = activeTab === 'transit'
        ? ACTIVE_STATUSES.includes(transfer.status)
        : FINAL_STATUSES.includes(transfer.status);
      if (!belongsToTab || !term) return belongsToTab;

      const searchableText = [
        transfer.transferNumber,
        transfer.dispatchJvId,
        transfer.projectName,
        transfer.sourceLocationName,
        transfer.destinationLocationName,
        transfer.status,
        transfer.notes,
        ...transfer.items.flatMap((item) => [
          item.productName,
          item.categoryName,
          item.receiptJournalVoucherId,
          item.cancellationJournalVoucherId,
        ]),
      ].filter((value) => value != null).join(' ').toLowerCase();

      return searchableText.includes(term);
    });
  }, [transfers, activeTab, resultSearch]);

  const openReceive = async (transfer) => {
    clearMessages();
    const pendingItems = transfer.items.filter((item) => item.lineState === 'Pending');
    setReceiveTransfer(transfer);
    setReceiptDate(today());
    setSelectedReceiptProducts(Object.fromEntries(
      pendingItems.map((item) => [item.productAccountId, true]),
    ));
    setCharges([]);
    try {
      await loadNormalAccounts(transfer.projectId);
    } catch (err) {
      showApiError(err);
    }
  };

  const selectedReceiptItems = useMemo(() =>
    (receiveTransfer?.items || []).filter((item) =>
      item.lineState === 'Pending' && selectedReceiptProducts[item.productAccountId]),
  [receiveTransfer, selectedReceiptProducts]);

  const updateCharge = (index, field, value) => setCharges((previous) =>
    previous.map((charge, chargeIndex) =>
      chargeIndex === index ? { ...charge, [field]: value } : charge));

  const changeManualAllocation = (chargeIndex, productAccountId, value) => {
    setCharges((previous) => previous.map((charge, index) => {
      if (index !== chargeIndex) return charge;
      const current = calculateAllocations(charge, selectedReceiptItems);
      return {
        ...charge,
        allocationMethod: 'Manual',
        allocations: { ...current, [productAccountId]: value },
      };
    }));
  };

  const validateReceipt = () => {
    if (!selectedReceiptItems.length) return 'Select at least one complete product line to receive.';
    for (const charge of charges) {
      if (!charge.payeeAccountId || Number(charge.amount) <= 0) {
        return 'Every charge requires a payee and positive amount.';
      }
      if (charge.targetType === 'OtherAccount') {
        if (!charge.targetAccountId) return 'Select the debit account for each account-targeted charge.';
        if (String(charge.targetAccountId) === String(charge.payeeAccountId)) {
          return 'A charge target account and payee account must differ.';
        }
      } else {
        const selectedIds = selectedReceiptItems.map((item) => item.productAccountId);
        if (!selectedIds.length) return 'Select at least one received product for each product charge.';
        const allocations = calculateAllocations(charge, selectedReceiptItems);
        const total = round4(selectedIds.reduce((sum, id) => sum + Number(allocations[id] || 0), 0));
        if (Math.abs(total - round4(charge.amount)) > 0.0001) {
          return 'Product allocations must equal the charge amount to four decimal places.';
        }
      }
    }
    return null;
  };

  const submitReceipt = async () => {
    clearMessages();
    const validationError = validateReceipt();
    if (validationError) {
      setError(validationError);
      return;
    }
    setLoading(true);
    try {
      const response = await apiClient.post(`${API}/${receiveTransfer.id}/receive`, {
        expectedRowVersion: receiveTransfer.rowVersion,
        receiptDate,
        selectedProductAccountIds: selectedReceiptItems.map((item) => item.productAccountId),
        charges: charges.map((charge) => {
          const selectedIds = selectedReceiptItems.map((item) => item.productAccountId);
          const allocations = calculateAllocations(charge, selectedReceiptItems);
          return {
            payeeAccountId: Number(charge.payeeAccountId),
            amount: Number(charge.amount),
            description: charge.description || null,
            targetType: charge.targetType,
            allocationMethod: charge.targetType === 'Products' ? charge.allocationMethod : 'Manual',
            targetAccountId: charge.targetType === 'OtherAccount'
              ? Number(charge.targetAccountId)
              : null,
            selectedProductAccountIds: charge.targetType === 'Products' ? selectedIds : [],
            allocations: charge.targetType === 'Products'
              ? selectedIds.map((productAccountId) => ({
                productAccountId,
                amount: Number(allocations[productAccountId] || 0),
              }))
              : [],
          };
        }),
      });
      const result = unwrap(response);
      const receiptJvId = result.receiptJournalVoucherId
        || result.receiptJvId
        || result.receipts?.at(-1)?.receiptJournalVoucherId;
      setReceiveTransfer(null);
      await loadTransfers();
      setSuccess(receiptJvId
        ? `Receipt JV ${receiptJvId} posted successfully.`
        : 'Selected products were received successfully.');
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const cancelRemaining = async (transfer) => {
    const action = transfer.status === 'PartiallyReceived' ? 'close short' : 'cancel';
    if (!window.confirm(
      `This will ${action} ${transfer.transferNumber} and return every pending product to ${transfer.sourceLocationName}. Continue?`,
    )) return;
    clearMessages();
    setLoading(true);
    try {
      await apiClient.post(`${API}/${transfer.id}/cancel`, {
        expectedRowVersion: transfer.rowVersion,
      });
      setDetails(null);
      await loadTransfers();
      setSuccess(transfer.status === 'PartiallyReceived'
        ? 'Pending products were returned to source and the transfer is Closed Short.'
        : 'All products were returned to source and the transfer is Cancelled.');
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const openDelete = (transfer) => {
    setDeleteTarget(transfer);
    setDeleteReason('');
  };

  const deleteEntireTransfer = async () => {
    if (deleteReason.trim().length < 3) {
      setError('Enter a deletion reason of at least three characters.');
      return;
    }
    setLoading(true);
    clearMessages();
    try {
      await apiClient.post(`${API}/${deleteTarget.id}/delete`, {
        expectedRowVersion: deleteTarget.rowVersion,
        reason: deleteReason.trim(),
      });
      setDeleteTarget(null);
      setDetails(null);
      await loadTransfers();
      setSuccess('The complete stock-transfer graph was deleted.');
    } catch (err) {
      showApiError(err);
    } finally {
      setLoading(false);
    }
  };

  const FilterBar = () => (
    <Card as={Form} className="shadow mb-3 transfer-filter-card" onSubmit={(event) => {
      event.preventDefault();
      setResultSearch('');
      loadTransfers();
    }}>
      <Card.Header className="py-2 d-flex justify-content-between align-items-center">
        <h6 className="m-0 fw-semibold text-primary">
          <i className="fas fa-filter me-2" />
          {activeTab === 'transit' ? 'In-transit filters' : 'History filters'}
        </h6>
      </Card.Header>
      <Card.Body>
        <Row className="g-3 align-items-end">
          <Col xl={2} md={4}>
            <Form.Label className="small">Since</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-calendar-alt" /></InputGroup.Text>
              <Form.Control
                type="date"
                aria-label="Dispatch date from"
                value={filters.dispatchFromDate}
                onChange={(event) => setFilters({ ...filters, dispatchFromDate: event.target.value })}
              />
            </InputGroup>
          </Col>
          <Col xl={2} md={4}>
            <Form.Label className="small">To</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-calendar-alt" /></InputGroup.Text>
              <Form.Control
                type="date"
                min={filters.dispatchFromDate || undefined}
                aria-label="Dispatch date to"
                value={filters.dispatchToDate}
                onChange={(event) => setFilters({ ...filters, dispatchToDate: event.target.value })}
              />
            </InputGroup>
          </Col>
          <Col xl={2} md={4}>
            <Form.Label>Project</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-project-diagram" /></InputGroup.Text>
              <SearchableSelect
                size="md"
                value={filters.projectId}
                onChange={(value) => setFilters((previous) => ({ ...previous, projectId: value || '', categoryId: '' }))}
                options={init.projects || []}
                placeholder="All projects"
                onClear={() => setFilters((previous) => ({ ...previous, projectId: '', categoryId: '' }))}
              />
            </InputGroup>
          </Col>
          <Col xl={2} md={4}>
            <Form.Label>Warehouse</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-warehouse" /></InputGroup.Text>
              <SearchableSelect
                size="md"
                value={filters.locationId}
                onChange={(value) => setFilters((previous) => ({ ...previous, locationId: value || '' }))}
                options={init.locations || []}
                placeholder="All warehouses"
                onClear={() => setFilters((previous) => ({ ...previous, locationId: '' }))}
              />
            </InputGroup>
          </Col>
          <Col xl={2} md={4}>
            <Form.Label>Category</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-list" /></InputGroup.Text>
              <SearchableSelect
                size="md"
                value={filters.categoryId}
                onChange={(value) => setFilters((previous) => ({ ...previous, categoryId: value || '' }))}
                options={filterCategories}
                placeholder="All categories"
                disabled={!filters.projectId}
                onClear={() => setFilters((previous) => ({ ...previous, categoryId: '' }))}
              />
            </InputGroup>
          </Col>
          <Col xl={2} xs={12} className="filter-submit-column">
            <Button
              type="submit"
              className="filter-action-button"
              disabled={!filters.projectId || loading}
              title={!filters.projectId ? 'Select a project to search' : ''}
            >
              <i className="fas fa-search me-2" />
              Search
            </Button>
          </Col>
        </Row>
      </Card.Body>
    </Card>
  );

  const TransferActions = ({ transfer }) => (
    <div className="transfer-actions">
      {ACTIVE_STATUSES.includes(transfer.status) && (
        <>
          <Button size="sm" variant="success" onClick={() => openReceive(transfer)}>
            <i className="fas fa-check me-1" />
            Receive
          </Button>
          <Button size="sm" variant="outline-warning" onClick={() => cancelRemaining(transfer)}>
            <i className={`fas ${transfer.status === 'PartiallyReceived' ? 'fa-box' : 'fa-times'} me-1`} />
            {transfer.status === 'PartiallyReceived' ? 'Close short' : 'Cancel'}
          </Button>
        </>
      )}
      {transfer.status === 'Pending' && (
        <Button size="sm" variant="outline-primary" onClick={() => beginEdit(transfer)}>
          <i className="fas fa-edit me-1" />
          Edit
        </Button>
      )}
      <Button size="sm" variant="outline-danger" onClick={() => openDelete(transfer)}>
        <i className="fas fa-trash me-1" />
        Delete entire
      </Button>
    </div>
  );

  const TransferTable = () => (
    <Card className="shadow mb-3">
      <Card.Header className="py-2 d-flex justify-content-between align-items-center">
        <h6 className="m-0 font-weight-bold text-primary">
          <i className={`fas ${activeTab === 'transit' ? 'fa-truck-loading' : 'fa-history'} me-2`} />
          {activeTab === 'transit' ? 'In-Transit Transfers' : 'Transfer History'}
        </h6>
        <Badge bg="primary">
          {loading
            ? `${transfers.length} of ${transferTotalCount || '…'}`
            : filteredByTab.length}
        </Badge>
      </Card.Header>
      <Card.Body className="p-0">
        <div className="transfer-output-search p-3 border-bottom">
          <Form.Label>Search</Form.Label>
          <InputGroup>
            <InputGroup.Text><i className="fas fa-search" /></InputGroup.Text>
            <Form.Control
              value={resultSearch}
              onChange={(event) => setResultSearch(event.target.value)}
              placeholder="Transfer, JV, product, route, status or notes"
              aria-label="Search loaded stock transfer results"
              disabled={loading}
            />
          </InputGroup>
        </div>
        <div className="table-responsive">
          <Table bordered hover className="mb-0 align-middle transfer-table">
            <thead className="table-primary">
              <tr>
                <th>Transfer</th>
                <th>Dispatch</th>
                <th>Project</th>
                <th>Route</th>
                <th>Products</th>
                <th>Sent</th>
                <th>Received</th>
                <th>Pending</th>
                <th>Pending value</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {loading && !transfers.length ? (
                <tr><td colSpan="11" className="text-center py-5"><Spinner /></td></tr>
              ) : filteredByTab.length === 0 ? (
                <tr><td colSpan="11" className="text-center py-5 text-muted">No transfers found.</td></tr>
              ) : filteredByTab.map((transfer) => (
                <tr key={transfer.id}>
                  <td>
                    <button className="btn btn-link p-0 fw-semibold" onClick={() => showDetails(transfer.id)}>
                      {transfer.transferNumber}
                    </button>
                    <div className="small text-muted">Dispatch JV {transfer.dispatchJvId}</div>
                  </td>
                  <td>{transfer.transferDate || transfer.dispatchDate}</td>
                  <td>{transfer.projectName}</td>
                  <td className="route-cell">
                    {transfer.sourceLocationName}
                    <i className="fas fa-arrow-right mx-2 text-muted" />
                    {transfer.destinationLocationName}
                  </td>
                  <td>{transfer.items.length}</td>
                  <td>{transfer.totalDispatchedQuantity}</td>
                  <td>{transfer.totalReceivedQuantity}</td>
                  <td className="fw-semibold">{transfer.totalRemainingQuantity}</td>
                  <td>{money(transfer.remainingValue)}</td>
                  <td><Badge bg={statusVariant(transfer.status)}>{transfer.status}</Badge></td>
                  <td><TransferActions transfer={transfer} /></td>
                </tr>
              ))}
            </tbody>
          </Table>
        </div>
      </Card.Body>
    </Card>
  );

  const detailSummary = details?.summary;

  return (
    <div className="stock-transfer-page container-fluid py-3">
      {/* A message drawn on the page behind an open dialog is invisible, so each dialog shows the error itself. */}
      {error && !receiveTransfer && !details && !deleteTarget && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert variant="success" dismissible onClose={() => setSuccess('')}>{success}</Alert>}

      <Tabs
        activeKey={activeTab}
        onSelect={(key) => {
          setActiveTab(key);
          setResultSearch('');
        }}
        className="mb-3"
      >
        <Tab eventKey="new" title={<><i className="fas fa-plus-circle me-2" />{editing ? 'Edit Pending' : 'New Transfer'}</>}>
          {editing && (
            <Alert variant="info" className="d-flex justify-content-between align-items-center">
              <span>
                Editing <strong>{editing.transferNumber}</strong>. The dispatch JV ID stays unchanged.
              </span>
              <Button
                size="sm"
                variant="outline-secondary"
                onClick={() => {
                  const restoredProjectId = resolveLastSelectedProject(init.projects);
                  setForm({
                    projectId: restoredProjectId,
                    sourceLocationId: '',
                    destinationLocationId: '',
                    transferDate: today(),
                    notes: '',
                    items: [blankItem()],
                  });
                  void onProjectChange(restoredProjectId, false);
                }}
              >
                <i className="fas fa-times me-1" />
                Stop editing
              </Button>
            </Alert>
          )}
          <Form onSubmit={submitTransfer}>
            <Card className="shadow mb-3">
              <Card.Header className="py-2">
                <h6 className="m-0 font-weight-bold text-primary">
                  <i className="fas fa-info-circle me-2" />
                  Transfer Details
                </h6>
              </Card.Header>
              <Card.Body className="py-2">
                <Row className="g-3 align-items-end">
                  <Col xl={2} md={6}>
                    <Form.Label>Project</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-project-diagram" /></InputGroup.Text>
                      <SearchableSelect
                        size="md"
                        value={form.projectId}
                        onChange={(value) => onProjectChange(value || '')}
                        options={init.projects || []}
                        placeholder="Search project"
                        disabled={!!editing}
                        onClear={() => onProjectChange('')}
                      />
                    </InputGroup>
                  </Col>
                  <Col xl={2} md={6}>
                    <Form.Label>Source warehouse</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-warehouse" /></InputGroup.Text>
                      <SearchableSelect
                        size="md"
                        value={form.sourceLocationId}
                        onChange={(value) => updateHeader('sourceLocationId', value || '')}
                        options={init.locations || []}
                        placeholder="Search source"
                        onClear={() => updateHeader('sourceLocationId', '')}
                      />
                    </InputGroup>
                  </Col>
                  <Col xl={2} md={6}>
                    <Form.Label>Destination warehouse</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-map-marker-alt" /></InputGroup.Text>
                      <SearchableSelect
                        size="md"
                        value={form.destinationLocationId}
                        onChange={(value) => updateHeader('destinationLocationId', value || '')}
                        options={(init.locations || [])
                          ?.filter((location) => String(location.id) !== String(form.sourceLocationId))
                        }
                        placeholder="Search destination"
                        onClear={() => updateHeader('destinationLocationId', '')}
                      />
                    </InputGroup>
                  </Col>
                  <Col xl={2} md={6}>
                    <Form.Label>Dispatch date</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-calendar-alt" /></InputGroup.Text>
                      <Form.Control
                        type="date"
                        max={today()}
                        value={form.transferDate}
                        onChange={(event) => updateHeader('transferDate', event.target.value)}
                        required
                      />
                    </InputGroup>
                  </Col>
                  <Col xl={4} md={12}>
                    <Form.Label>Notes / reference</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-sticky-note" /></InputGroup.Text>
                      <Form.Control
                        value={form.notes}
                        maxLength={500}
                        onChange={(event) => setForm({ ...form, notes: event.target.value })}
                        placeholder="Repeated into JV descriptions and delivery-challan remarks"
                      />
                    </InputGroup>
                  </Col>
                </Row>
              </Card.Body>
            </Card>
            {form.projectId && (
              <Card className={`shadow mb-3 transit-setting-card ${transitSettings?.isConfigured ? 'is-configured' : 'needs-configuration'}`}>
                <Card.Body>
                  <div className="d-flex flex-wrap justify-content-between align-items-start gap-3">
                    <div>
                      <div className="fw-semibold">
                        <i className={`fas ${transitSettings?.isConfigured ? 'fa-check-circle text-success' : 'fa-exclamation-triangle text-warning'} me-2`} />
                        Stock In Transit posting account
                      </div>
                      {transitSettings?.isConfigured ? (
                        <div className="text-muted small mt-1">
                          {transitSettings.inTransitCategoryName} / {transitSettings.inTransitAccountName}
                        </div>
                      ) : (
                        <div className="text-muted small mt-1">
                          This project must be configured before its first dispatch.
                          {!user?.isAdmin && ' Ask an administrator to complete this setup.'}
                        </div>
                      )}
                    </div>
                    {user?.isAdmin && (
                      <div className="transit-setting-controls">
                        <SearchableSelect
                          size="md"
                          value={transitSettingForm.inTransitCategoryId}
                          onChange={(value) => changeTransitCategory(value || '')}
                          options={transitCategories}
                          placeholder="Search transit category"
                          onClear={() => changeTransitCategory('')}
                        />
                        <SearchableSelect
                          size="md"
                          value={transitSettingForm.inTransitAccountId}
                          onChange={(value) => setTransitSettingForm({
                            ...transitSettingForm,
                            inTransitAccountId: value || '',
                          })}
                          options={transitAccounts}
                          placeholder="Select transit account"
                          disabled={!transitSettingForm.inTransitCategoryId}
                        />
                        <Button
                          variant="outline-primary"
                          disabled={loading || !transitSettingForm.inTransitAccountId}
                          onClick={saveTransitSettings}
                        >
                          <i className="fas fa-save me-1" />
                          Save setting
                        </Button>
                      </div>
                    )}
                  </div>
                </Card.Body>
              </Card>
            )}
            <Card className="shadow mb-3">
              <Card.Header className="py-2 d-flex justify-content-between align-items-center">
                <h6 className="m-0 font-weight-bold text-primary">
                  <i className="fas fa-boxes me-2" />
                  Products
                </h6>
                <Button
                  size="sm"
                  variant="outline-primary"
                  onClick={addItem}
                >
                  <i className="fas fa-plus me-1" />
                  Add product (Alt+N)
                </Button>
              </Card.Header>
              <Card.Body className="p-0">
                <div className="table-responsive">
                  <Table bordered className="mb-0 align-middle product-entry-table">
                    <thead className="table-primary">
                      <tr>
                        <th>Category</th>
                        <th>Product</th>
                        <th>Available</th>
                        <th>Whole quantity</th>
                        <th title="Suggested from source cost; an entered price sets destination valuation">Transfer cost</th>
                        <th>Base value</th>
                        <th />
                      </tr>
                    </thead>
                    <tbody ref={itemsTableBodyRef}>
                      {/* Rows are keyed by position only: a key that includes the chosen product recreates the row when a
                          product is selected, dropping keyboard focus so the next Tab jumps back to the top of the page. */}
                      {form.items.map((item, index) => (
                        <tr key={index}>
                          <td>
                            <SearchableSelect
                              size="md"
                              value={item.categoryId}
                              onChange={(value) => loadRowAccounts(index, value || '')}
                              options={categories}
                              placeholder="Search category"
                              disabled={!form.projectId || !form.sourceLocationId || !form.destinationLocationId}
                              onClear={() => loadRowAccounts(index, '')}
                            />
                          </td>
                          <td className="product-select-cell">
                            <SearchableSelect
                              size="md"
                              value={item.productAccountId}
                              onChange={(value) => loadAvailability(index, value)}
                              options={item.accounts}
                              placeholder="Select product"
                              disabled={!item.categoryId || !form.sourceLocationId}
                            />
                          </td>
                          <td className={Number(item.availableQuantity) < Number(item.quantity) ? 'text-danger fw-semibold' : ''}>
                            {item.availableQuantity ?? '—'}
                          </td>
                          <td>
                            <Form.Control
                              type="number"
                              min="1"
                              step="1"
                              value={item.quantity}
                              onChange={(event) => updateItem(index, 'quantity', event.target.value)}
                            />
                          </td>
                          <td>
                            <div className="cost-input-stack">
                              <Form.Control
                                type="number"
                                min="0.0001"
                                step="0.0001"
                                value={item.unitCost}
                                onChange={(event) => updateItem(index, 'unitCost', event.target.value)}
                                aria-label="Transfer cost"
                              />
                              {item.unitCostOverridden && item.suggestedUnitCost != null && (
                                <Button type="button" variant="link" size="sm" className="p-0 text-decoration-none"
                                  onClick={() => setForm(previous => ({ ...previous, items: previous.items.map((candidate, itemIndex) =>
                                    itemIndex === index ? { ...candidate, unitCost: candidate.suggestedUnitCost, unitCostOverridden: false } : candidate) }))}>
                                  Use suggested
                                </Button>
                              )}
                            </div>
                          </td>
                          <td className="fw-semibold">{money(transferCarryingValue(item))}</td>
                          <td>
                            <Button
                              size="sm"
                              variant="outline-danger"
                              disabled={form.items.length === 1}
                              onClick={() => setForm((previous) => ({
                                ...previous,
                                items: previous.items.filter((_, itemIndex) => itemIndex !== index),
                              }))}
                              aria-label="Remove product"
                            >
                              <i className="fas fa-trash" />
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                </div>
              </Card.Body>
              <Card.Footer className="d-flex justify-content-between align-items-center">
                <strong>
                  Total base value: {money(form.items.reduce(
                    (sum, item) => sum + transferCarryingValue(item),
                    0,
                  ))}
                </strong>
                <Button type="submit" variant="primary" disabled={loading || !transitSettings?.isConfigured}>
                  {loading ? <Spinner size="sm" /> : (
                    <>
                      <i className={`fas ${editing ? 'fa-save' : 'fa-truck-loading'} me-2`} />
                      {editing ? 'Save pending transfer' : 'Dispatch transfer'}
                    </>
                  )}
                </Button>
              </Card.Footer>
            </Card>
          </Form>
        </Tab>
        <Tab eventKey="transit" title={<><i className="fas fa-truck-loading me-2" />In Transit</>}>
          <FilterBar />
          <TransferTable />
        </Tab>
        <Tab eventKey="history" title={<><i className="fas fa-history me-2" />History</>}>
          <FilterBar />
          <TransferTable />
        </Tab>
      </Tabs>

      <Modal
        show={!!receiveTransfer}
        onHide={() => { setReceiveTransfer(null); setError(''); }}
        size="xl"
        centered
        scrollable
        className="stock-transfer-receive-modal"
        dialogClassName="stock-transfer-receive-dialog"
      >
        <Modal.Header closeButton>
          <Modal.Title>
            <i className="fas fa-truck-loading text-success me-2" />
            Receive {receiveTransfer?.transferNumber}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
          <Alert variant="light" className="border">
            <i className="fas fa-info-circle text-primary me-2" />
            Products are atomic: selecting a line receives its complete dispatched quantity and base value.
          </Alert>
          <Row className="g-3 align-items-end mb-3">
            <Col md={4}>
              <Form.Label>Receipt date</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-calendar-alt" /></InputGroup.Text>
                <Form.Control
                  type="date"
                  min={receiveTransfer?.transferDate || receiveTransfer?.dispatchDate}
                  max={today()}
                  value={receiptDate}
                  onChange={(event) => setReceiptDate(event.target.value)}
                />
              </InputGroup>
            </Col>
            <Col className="receive-route text-muted">
              <i className="fas fa-warehouse me-2" />
              {receiveTransfer?.sourceLocationName}
              <i className="fas fa-arrow-right mx-2" />
              {receiveTransfer?.destinationLocationName}
            </Col>
          </Row>
          <h6>Pending products</h6>
          <Table responsive bordered size="sm">
            <thead className="table-primary">
              <tr>
                <th className="text-center">Receive</th>
                <th>Product</th>
                <th>Full quantity</th>
                <th>Unit cost</th>
                <th>Base value</th>
              </tr>
            </thead>
            <tbody>
              {receiveTransfer?.items
                .filter((item) => item.lineState === 'Pending')
                .map((item) => (
                  <tr key={item.productAccountId}>
                    <td className="text-center">
                      <Form.Check
                        checked={!!selectedReceiptProducts[item.productAccountId]}
                        onChange={(event) => setSelectedReceiptProducts({
                          ...selectedReceiptProducts,
                          [item.productAccountId]: event.target.checked,
                        })}
                        aria-label={`Receive ${item.productName}`}
                      />
                    </td>
                    <td>{item.productName}</td>
                    <td>{item.quantity}</td>
                    <td>{money(item.unitCost)}</td>
                    <td>{money(item.baseValue)}</td>
                  </tr>
                ))}
            </tbody>
          </Table>
          <div className="text-end fw-semibold mb-4">
            Selected base value: {money(selectedReceiptItems.reduce((sum, item) => sum + item.baseValue, 0))}
          </div>

          <div className="d-flex justify-content-between align-items-center mb-2">
            <h6 className="mb-0">
              Receiving charges <Badge bg="success">{charges.length}</Badge>
            </h6>
            <Button
              size="sm"
              variant="outline-primary"
              disabled={!selectedReceiptItems.length}
              onClick={() => setCharges([
                ...charges,
                blankCharge(),
              ])}
            >
              <i className="fas fa-plus me-1" />
              Add charge
            </Button>
          </div>
          {charges.map((charge, index) => {
            const preview = calculateAllocations(charge, selectedReceiptItems);
            return (
              <Card className="mb-2 charge-card" key={index}>
                <Card.Header className="charge-card-header">
                  <div>
                    <strong>Charge {index + 1}</strong>
                    <span className="text-muted small ms-2">Separate linked JV</span>
                  </div>
                  <Button
                    size="sm"
                    variant="outline-danger"
                    onClick={() => setCharges(charges.filter((_, chargeIndex) => chargeIndex !== index))}
                    aria-label={`Remove charge ${index + 1}`}
                  >
                    <i className="fas fa-trash me-1" />
                    Remove
                  </Button>
                </Card.Header>
                <Card.Body>
                  <Row className="g-2 align-items-end">
                    <Col xl={4} md={7}>
                      <Form.Label>Paid to account</Form.Label>
                      <SearchableSelect
                        size="md"
                        value={charge.payeeAccountId}
                        onChange={(value) => updateCharge(index, 'payeeAccountId', value || '')}
                        options={normalAccounts}
                        placeholder="Search account to credit"
                      />
                    </Col>
                    <Col xl={2} md={5}>
                      <Form.Label>Amount</Form.Label>
                      <Form.Control
                        type="number"
                        min="0.0001"
                        step="0.0001"
                        value={charge.amount}
                        onChange={(event) => updateCharge(index, 'amount', event.target.value)}
                      />
                    </Col>
                    <Col xl={3} md={6}>
                      <Form.Label>Add charge to</Form.Label>
                      <div className="charge-target-toggle" role="group" aria-label="Charge target">
                        <Button
                          type="button"
                          variant={charge.targetType === 'Products' ? 'primary' : 'outline-secondary'}
                          onClick={() => updateCharge(index, 'targetType', 'Products')}
                        >
                          Products
                        </Button>
                        <Button
                          type="button"
                          variant={charge.targetType === 'OtherAccount' ? 'primary' : 'outline-secondary'}
                          onClick={() => updateCharge(index, 'targetType', 'OtherAccount')}
                        >
                          Other account
                        </Button>
                      </div>
                    </Col>
                    <Col xl={3} md={6}>
                      {charge.targetType === 'Products' ? (
                        <>
                          <Form.Label>Allocation</Form.Label>
                          <Form.Select value={charge.allocationMethod} onChange={(event) => updateCharge(index, 'allocationMethod', event.target.value)}>
                            <option value="Equal">Equal split</option>
                            <option value="ByQuantity">By quantity</option>
                            <option value="ByRate">By unit rate</option>
                            <option value="ByValue">By base value</option>
                            <option value="Manual">Manual</option>
                          </Form.Select>
                        </>
                      ) : (
                        <>
                          <Form.Label>Debit account</Form.Label>
                          <SearchableSelect
                            size="md"
                            value={charge.targetAccountId}
                            onChange={(value) => updateCharge(index, 'targetAccountId', value || '')}
                            options={normalAccounts}
                            placeholder="Select debit account"
                          />
                        </>
                      )}
                    </Col>
                    <Col xs={12}>
                      <Form.Label>Notes / reference</Form.Label>
                      <Form.Control
                        value={charge.description}
                        onChange={(event) => updateCharge(index, 'description', event.target.value)}
                        placeholder="Optional details for this charge JV"
                      />
                    </Col>
                    {charge.targetType === 'Products' && (
                      <Col xs={12}>
                        <div className="allocation-heading">
                          <span>
                            Added to all {selectedReceiptItems.length} receiving product
                            {selectedReceiptItems.length === 1 ? '' : 's'}
                          </span>
                          <span className="text-muted">
                            Total allocated: {money(Object.values(preview).reduce(
                              (sum, value) => sum + Number(value || 0),
                              0,
                            ))}
                          </span>
                        </div>
                        <div className="allocation-grid">
                          {selectedReceiptItems.map((item) => (
                            <div key={item.productAccountId} className="allocation-item">
                              <div className="allocation-product">
                                <strong>{item.productName}</strong>
                                <span>
                                  Qty {item.quantity} · Rate {money(item.unitCost)} · Value {money(item.baseValue)}
                                </span>
                              </div>
                              <Form.Control
                                size="sm"
                                type="number"
                                step="0.0001"
                                value={charge.allocationMethod === 'Manual'
                                  ? charge.allocations[item.productAccountId] ?? ''
                                  : Number(preview[item.productAccountId] || 0).toFixed(4)}
                                onChange={(event) => changeManualAllocation(index, item.productAccountId, event.target.value)}
                                title="Editing a calculated split switches this charge to manual allocation"
                              />
                            </div>
                          ))}
                        </div>
                      </Col>
                    )}
                  </Row>
                </Card.Body>
              </Card>
            );
          })}
          <div className="text-end fw-semibold">
            Total charges: {money(charges.reduce((sum, charge) => sum + Number(charge.amount || 0), 0))}
          </div>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => { setReceiveTransfer(null); setError(''); }}>
            <i className="fas fa-times me-1" />
            Cancel
          </Button>
          <Button variant="success" onClick={submitReceipt} disabled={loading || !selectedReceiptItems.length}>
            {loading ? <Spinner size="sm" /> : (
              <>
                <i className="fas fa-check me-1" />
                Confirm received
              </>
            )}
          </Button>
        </Modal.Footer>
      </Modal>

      <Modal show={!!details} onHide={() => { setDetails(null); setError(''); }} size="xl" centered>
        <Modal.Header closeButton>
          <Modal.Title>{detailSummary?.transferNumber}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
          {detailSummary && (
            <>
              <div className="d-flex flex-wrap justify-content-between gap-2 mb-3">
                <div>
                  <strong>{detailSummary.sourceLocationName}</strong>
                  <i className="fas fa-arrow-right mx-2 text-muted" />
                  <strong>{detailSummary.destinationLocationName}</strong>
                  <Badge className="ms-3" bg={statusVariant(detailSummary.status)}>{detailSummary.status}</Badge>
                </div>
                <div className="text-muted">
                  Dispatch JV {detailSummary.dispatchJvId}
                  {detailSummary.dispatchDeliveryChallanId && ` · DC ${detailSummary.dispatchDeliveryChallanId}`}
                </div>
              </div>
              {(details.notes || detailSummary.notes) && <p>{details.notes || detailSummary.notes}</p>}
              <Table bordered responsive size="sm">
                <thead className="table-primary">
                  <tr>
                    <th>Product</th>
                    <th>State</th>
                    <th>Quantity</th>
                    <th>Unit cost</th>
                    <th>Base value</th>
                    <th>Receipt / cancellation</th>
                  </tr>
                </thead>
                <tbody>
                  {detailSummary.items.map((item) => (
                    <tr key={item.productAccountId}>
                      <td>{item.productName}</td>
                      <td><Badge bg={item.lineState === 'Received' ? 'success' : item.lineState === 'Cancelled' ? 'secondary' : 'warning'}>{item.lineState}</Badge></td>
                      <td>{item.quantity}</td>
                      <td>{money(item.unitCost)}</td>
                      <td>{money(item.baseValue)}</td>
                      <td>
                        {item.receiptJournalVoucherId && `Receipt JV ${item.receiptJournalVoucherId}`}
                        {item.cancellationJournalVoucherId && `Cancellation JV ${item.cancellationJournalVoucherId}`}
                        {!item.receiptJournalVoucherId && !item.cancellationJournalVoucherId && '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              {!!details.receipts?.length && (
                <>
                  <h6 className="mt-4">Receipts and linked charge JVs</h6>
                  {details.receipts.map((receipt) => (
                    <div className="receipt-line" key={receipt.receiptJournalVoucherId || receipt.receiptJvId}>
                      <div className="receipt-line-header">
                        <div>
                          <strong>{receipt.receiptDate}</strong>
                          {' · '}Receipt JV {receipt.receiptJournalVoucherId || receipt.receiptJvId}
                          {receipt.deliveryChallanId && ` · DC ${receipt.deliveryChallanId}`}
                        </div>
                        <Badge bg={(receipt.charges || []).length ? 'primary' : 'secondary'}>
                          {(receipt.charges || []).length} charge{(receipt.charges || []).length === 1 ? '' : 's'}
                        </Badge>
                      </div>
                      {(receipt.charges || []).length > 0 ? (
                        <div className="receipt-charge-list">
                          {receipt.charges.map((charge) => (
                            <div className="receipt-charge-item" key={charge.chargeJournalVoucherId}>
                              <div>
                                <strong>{charge.payeeAccountName}</strong>
                                <span className="text-muted">
                                  {' → '}
                                  {charge.targetType === 'Products'
                                    ? `${charge.allocations?.length || 0} product account${charge.allocations?.length === 1 ? '' : 's'}`
                                    : charge.allocations?.[0]?.accountName || 'Other account'}
                                </span>
                              </div>
                              <div className="text-end">
                                <strong>{money(charge.amount)}</strong>
                                <div className="small text-muted">Charge JV {charge.chargeJournalVoucherId}</div>
                              </div>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <div className="small text-muted">No receiving charges</div>
                      )}
                    </div>
                  ))}
                </>
              )}
            </>
          )}
        </Modal.Body>
        <Modal.Footer className="justify-content-between">
          {detailSummary && <TransferActions transfer={detailSummary} />}
          <Button variant="secondary" onClick={() => { setDetails(null); setError(''); }}>
            <i className="fas fa-times me-1" />
            Close
          </Button>
        </Modal.Footer>
      </Modal>

      <Modal show={!!deleteTarget} onHide={() => { setDeleteTarget(null); setError(''); }} centered>
        <Modal.Header closeButton>
          <Modal.Title>Delete entire stock transfer</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error && <Alert variant="danger" dismissible onClose={() => setError('')}>{error}</Alert>}
          <Alert variant="danger">
            This removes the dispatch, all receipts, all receiving-charge JVs, cancellation JV,
            delivery challans, entries, and typed references for <strong>{deleteTarget?.transferNumber}</strong>.
          </Alert>
          <Form.Label>Required deletion reason</Form.Label>
          <Form.Control
            as="textarea"
            rows={3}
            maxLength={500}
            value={deleteReason}
            onChange={(event) => setDeleteReason(event.target.value)}
            placeholder="Why is the complete transfer being deleted?"
          />
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => { setDeleteTarget(null); setError(''); }}>
            <i className="fas fa-times me-1" />
            Keep transfer
          </Button>
          <Button variant="danger" disabled={loading || deleteReason.trim().length < 3} onClick={deleteEntireTransfer}>
            {loading ? <Spinner size="sm" /> : (
              <>
                <i className="fas fa-trash me-1" />
                Delete complete graph
              </>
            )}
          </Button>
        </Modal.Footer>
      </Modal>
    </div>
  );
}
