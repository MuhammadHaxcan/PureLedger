import { addDays, todayString } from '../../utils/businessDate';
import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router-dom';
import { Card, Form, Button, Table, Alert, Modal } from 'react-bootstrap';
import apiClient from '../../services/api';
import approvalApi from '../../services/approvalApi';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import { LAST_PROJECT_STORAGE_KEY } from '../../utils/lastSelectedProject';
import { formatMoney, formatMoneyInput, parseMoneyInput } from '../../utils/moneyFormat';
import { ACCOUNT_GROUP, isAccountGroup } from '../../constants/accountGroups';
import './Entry.css';

const LAST_JV_MESSAGE_KEY = 'pl_last_jv_message';

const positiveId = (value) => {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
};

const readPayload = (response) => response?.data?.data ?? response?.data ?? {};

const clearProductMechanics = (entry) => ({
    ...entry,
    pkg: null,
    rbp: null,
    qty: null,
    rate: null,
    percent: null,
    disc: null,
    dataPkg: null,
    totalAmount: 0
});

const HomeEntry = () => {
    const { approvalId: approvalIdParam } = useParams();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const editingVoucherId = positiveId(searchParams.get('voucherId'));
    const editingApprovalId = positiveId(approvalIdParam);
    const isVoucherEditMode = editingVoucherId != null;
    const isApprovalEditMode = editingApprovalId != null;
    const isEditMode = isVoucherEditMode || isApprovalEditMode;
    const isBookerRequired = !isApprovalEditMode;

    usePageTitle(
        isApprovalEditMode
            ? 'Edit Pending Voucher'
            : (isVoucherEditMode ? 'Edit Entry' : 'Entry'),
        isApprovalEditMode
            ? `Edit Pending Voucher #${editingApprovalId}`
            : (isVoucherEditMode ? `Edit Journal Voucher #${editingVoucherId}` : 'Journal Entry')
    );

    const isSubmittingRef = useRef(false);

    const [projects, setProjects] = useState([]);
    const [bookers, setBookers] = useState([]);
    const [selectedProjectId, setSelectedProjectId] = useState(null);
    const [voucherVersion, setVoucherVersion] = useState(null);
    const [entryDate, setEntryDate] = useState(todayString());
    const [creditDays, setCreditDays] = useState(0);
    const [dueDate, setDueDate] = useState(todayString());
    const [bookerId, setBookerId] = useState(null);
    const [requireQuantity, setRequireQuantity] = useState(true);
    const [isBaseVoucher, setIsBaseVoucher] = useState(false);
    const [saveAsRecipe, setSaveAsRecipe] = useState(false);
    const [recipeName, setRecipeName] = useState('');
    const [entries, setEntries] = useState([{
        entryId: null,
        categoryId: null,
        accountId: null,
        pkg: 0,
        rbp: 'Y',
        des: '',
        qty: null,
        rate: null,
        percent: 0,
        disc: 0,
        debit: null,
        credit: null,
        accountGroup: null,
        accountType: null,
        dataPkg: null,
        totalAmount: 0
    }]);

    const [rowCategories, setRowCategories] = useState({});
    const [rowAccounts, setRowAccounts] = useState({});

    const [loading, setLoading] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [validationErrors, setValidationErrors] = useState([]);
    const [hasBookers, setHasBookers] = useState(false);
    const [bookersLoaded, setBookersLoaded] = useState(false);
    const [isEditingManualExpenseVoucher, setIsEditingManualExpenseVoucher] = useState(false);
    const [showExpenseAccountRequiredModal, setShowExpenseAccountRequiredModal] = useState(false);

    const persistedSuccessRef = useRef(null);
    const [focusedFields, setFocusedFields] = useState({});
    const [moneyInputValues, setMoneyInputValues] = useState({});
    const [hoveredDescIndex, setHoveredDescIndex] = useState(null);
    const hasExpenseAccount = entries.some(entry =>
        isAccountGroup(entry.accountGroup, ACCOUNT_GROUP.EXPENSE)
    );
    const entryCount = entries.length;

    useEffect(() => {
        if (isApprovalEditMode) {
            loadApprovalForEdit();
            return;
        }

        loadEntryData();
        if (isVoucherEditMode) {
            loadVoucherForEdit();
        }
        // These loaders intentionally run once for the route mode present at mount.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        if (error || validationErrors.length > 0) {
            localStorage.removeItem(LAST_JV_MESSAGE_KEY);
            persistedSuccessRef.current = null;
            return;
        }
        if (success && persistedSuccessRef.current && success !== persistedSuccessRef.current) {
            localStorage.removeItem(LAST_JV_MESSAGE_KEY);
            persistedSuccessRef.current = null;
            return;
        }
        const hasEntries = entries.length > 0 && entries.some(e => 
            e.categoryId || e.accountId || e.debit || e.credit || e.qty || e.rate || e.des
        );
        if (hasEntries && persistedSuccessRef.current && !success) {
            localStorage.removeItem(LAST_JV_MESSAGE_KEY);
            persistedSuccessRef.current = null;
            if (success) {
                setSuccess('');
            }
            return;
        }
        if (!success && !hasEntries) {
            const persistedMessage = localStorage.getItem(LAST_JV_MESSAGE_KEY);
            if (persistedMessage) {
                persistedSuccessRef.current = persistedMessage;
                setSuccess(persistedMessage);
            }
        }
    }, [success, error, validationErrors.length, entries]);

    useEffect(() => {
        if (selectedProjectId && !isEditMode) {
            const loadCategoriesForAllRows = async () => {
                const categories = await loadCategories(selectedProjectId, 0);
                const categoriesMap = {};
                for (let index = 0; index < entryCount; index += 1) {
                    categoriesMap[index] = categories;
                }
                setRowCategories(categoriesMap);
            };
            loadCategoriesForAllRows();
        }
    }, [selectedProjectId, entryCount, isEditMode]);

    useEffect(() => {
        if (entryDate) {
            const days = creditDays !== '' && creditDays !== null ? parseInt(creditDays) || 0 : 0;
            setDueDate(addDays(entryDate, days));
        }
    }, [entryDate, creditDays]);

    useEffect(() => {
        if (!hasExpenseAccount) return;

        setEntries(previous => previous.map(clearProductMechanics));
        setIsBaseVoucher(false);
        setSaveAsRecipe(false);
        setRecipeName('');
    }, [hasExpenseAccount]);


    const loadEntryData = async () => {
        try {
            const response = await apiClient.get('/HomeApi/entry-data');
            if (response.data.success) {
                setProjects(response.data.projects || []);
                setBookers(response.data.bookers || []);
                const bookersAvailable = (response.data.bookers || []).length > 0;
                setHasBookers(bookersAvailable);
                setBookersLoaded(true);
                const persistedProjectId = localStorage.getItem(LAST_PROJECT_STORAGE_KEY);
                const apiSelectedProjectId = response.data.selectedProjectId;
                const projectIdToUse = apiSelectedProjectId || (persistedProjectId ? parseInt(persistedProjectId) : null);
                if (projectIdToUse) {
                    setSelectedProjectId(projectIdToUse);
                } else if ((response.data.projects || []).length > 0) {
                    setSelectedProjectId(response.data.projects[0].id);
                }
                if (response.data.bookers && response.data.bookers.length > 0) {
                    setBookerId(response.data.bookers[0].id);
                }
            }
        } catch (error) {
            const errorMessage = error.message ||
                error.response?.data?.message ||
                'Failed to load entry data. Please try again.';
            setError(errorMessage);
            setBookersLoaded(true);
        }
    };

    const loadVoucherForEdit = async () => {
        try {
            setLoading(true);
            const response = await apiClient.get(`/HomeApi/edit-voucher/${editingVoucherId}`);
            if (response.data.success) {
                const {
                    model,
                    projects: projData,
                    bookers: bookData,
                    selectedProjectId: projId,
                    isManualExpenseVoucher,
                } = response.data;
                setIsEditingManualExpenseVoucher(Boolean(isManualExpenseVoucher));
                setVoucherVersion(model.voucherVersion ?? null);
                setProjects(projData || []);
                setBookers(bookData || []);
                const bookersAvailable = (bookData || []).length > 0;
                setHasBookers(bookersAvailable);
                setBookersLoaded(true);
                setSelectedProjectId(projId);
                setEntryDate(model.entryDate || todayString());
                setCreditDays(model.creditDays !== undefined && model.creditDays !== null ? model.creditDays : '');
                setDueDate(model.dueDate || todayString());
                setBookerId(model.bookerId);
                setRequireQuantity(model.requireQuantity !== false);
                setIsBaseVoucher(model.isBaseVoucher || false);

                if (model.entries && model.entries.length > 0) {
                    const categories = await loadCategories(projId, 0);
                    const categoriesMap = {};
                    const accountsMap = {};

                    const loadedEntries = await Promise.all(model.entries.map(async (entry, index) => {
                        let accountGroup = null;
                        let accountType = null;
                        let dataPkg = null;

                        categoriesMap[index] = categories;

                        if (entry.categoryId) {
                            const accounts = await loadAccounts(entry.categoryId, index);
                            accountsMap[index] = accounts;
                        }

                        if (entry.accountId) {
                            try {
                                const productRes = await apiClient.get(`/HomeApi/product-details/${entry.accountId}`);
                                if (productRes.data.success && productRes.data.data) {
                                    const product = productRes.data.data;
                                    accountGroup = Number(product.accountGroupId);
                                    accountType = product.accountTypeName || null;
                                    dataPkg = product.pkg || null;
                                }
                            } catch {
                                // Keep the persisted voucher values if account metadata is unavailable.
                            }
                        }

                        const parsedQty = entry.qty != null ? (typeof entry.qty === 'number' ? entry.qty : parseFloat(entry.qty)) : null;
                        const parsedRate = entry.rate != null ? (typeof entry.rate === 'number' ? entry.rate : parseFloat(entry.rate)) : null;
                        const parsedPercent = entry.percent != null ? (typeof entry.percent === 'number' ? entry.percent : parseFloat(entry.percent)) : 0;
                        const parsedDebit = entry.debit != null ? (typeof entry.debit === 'number' ? entry.debit : parseFloat(entry.debit)) : null;
                        const parsedCredit = entry.credit != null ? (typeof entry.credit === 'number' ? entry.credit : parseFloat(entry.credit)) : null;
                        const parsedPkg = entry.pkg != null ? (typeof entry.pkg === 'number' ? entry.pkg : parseFloat(entry.pkg)) : (dataPkg != null ? (typeof dataPkg === 'number' ? dataPkg : parseFloat(dataPkg)) : null);

                        const rbp = (entry.rbp || 'Y') === 'Y';
                        const qty = parsedQty || 0;
                        const rate = parsedRate || 0;
                        const pkg = parsedPkg || 0;

                        let calculatedTotalAmount = 0;
                        if (rbp && pkg > 0) {
                            calculatedTotalAmount = pkg * qty * rate;
                        } else {
                            calculatedTotalAmount = qty * rate;
                        }

                        const calculatedDisc = parseFloat(((calculatedTotalAmount * parsedPercent) / 100).toFixed(2));

                        return {
                            entryId: entry.entryId || null,
                            categoryId: entry.categoryId,
                            accountId: entry.accountId,
                            pkg: parsedPkg,
                            rbp: entry.rbp || 'Y',
                            des: entry.des || '',
                            qty: parsedQty,
                            rate: parsedRate,
                            percent: parsedPercent,
                            disc: calculatedDisc,
                            debit: parsedDebit,
                            credit: parsedCredit,
                            accountGroup,
                            accountType,
                            dataPkg,
                            totalAmount: calculatedTotalAmount
                        };
                    }));

                    setEntries(loadedEntries);
                    setRowCategories(categoriesMap);
                    setRowAccounts(accountsMap);
                }
            }
        } catch {
            setError('Failed to load voucher data');
        } finally {
            setLoading(false);
        }
    };

    const loadApprovalForEdit = async () => {
        try {
            setLoading(true);
            setError('');

            const [approvalResponse, entryDataResponse] = await Promise.all([
                approvalApi.getById(editingApprovalId),
                apiClient.get('/HomeApi/entry-data')
            ]);

            const approval = readPayload(approvalResponse);
            const entryData = readPayload(entryDataResponse);
            if (!approval?.approvalVoucherId) {
                throw new Error('The pending approval request was not found.');
            }
            if (String(approval.status || '').toLowerCase() !== 'pending') {
                throw new Error('Only pending approval requests can be edited.');
            }

            const projectId = Number(approval.projectId);
            const projectRows = entryData.projects || [];
            const bookerRows = entryData.bookers || [];
            setProjects(projectRows);
            setBookers(bookerRows);
            setHasBookers(bookerRows.length > 0);
            setBookersLoaded(true);
            setSelectedProjectId(projectId);
            setBookerId(approval.bookerId ?? null);
            setCreditDays(
                approval.creditDays !== undefined && approval.creditDays !== null
                    ? approval.creditDays
                    : 0
            );
            setEntryDate(todayString());
            setRequireQuantity(true);
            setIsBaseVoucher(false);
            setSaveAsRecipe(false);
            setRecipeName('');

            const sourceEntries = approval.entries || [];
            if (sourceEntries.length === 0) {
                throw new Error('The pending approval request does not contain any voucher entries.');
            }

            const categories = await loadCategories(projectId, 0);
            const categoriesMap = {};
            const accountsMap = {};
            const loadedEntries = await Promise.all(sourceEntries.map(async (entry, index) => {
                categoriesMap[index] = categories;
                if (entry.categoryId) {
                    accountsMap[index] = await loadAccounts(entry.categoryId, index);
                }

                let accountGroup = null;
                let accountType = null;
                let dataPkg = null;
                if (entry.accountId) {
                    try {
                        const productResponse = await apiClient.get(
                            `/HomeApi/product-details/${entry.accountId}`
                        );
                        const product = readPayload(productResponse);
                        accountGroup = Number(product.accountGroupId || 0);
                        accountType = product.accountTypeName || null;
                        dataPkg = product.pkg ?? null;
                    } catch {
                        // The selected account is still shown from the approval snapshot.
                        // Server validation will reject it if it is no longer available.
                    }
                }

                return {
                    entryId: entry.originalJournalEntryId ?? null,
                    categoryId: entry.categoryId ?? null,
                    accountId: entry.accountId ?? null,
                    pkg: null,
                    rbp: null,
                    des: entry.description ?? entry.des ?? '',
                    qty: null,
                    rate: null,
                    percent: null,
                    disc: null,
                    debit: entry.debit != null ? Number(entry.debit) : null,
                    credit: entry.credit != null ? Number(entry.credit) : null,
                    accountGroup,
                    accountType,
                    dataPkg,
                    totalAmount: 0
                };
            }));

            setEntries(loadedEntries);
            setRowCategories(categoriesMap);
            setRowAccounts(accountsMap);
        } catch (requestError) {
            setError(
                requestError.response?.data?.message ||
                requestError.message ||
                'Failed to load the pending voucher.'
            );
            setBookersLoaded(true);
        } finally {
            setLoading(false);
        }
    };

    const loadCategories = async (projectId) => {
        if (!projectId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
        } catch {
            // The caller renders an empty selector and preserves the current value.
        }
        return [];
    };

    const loadAccounts = async (categoryId) => {
        if (!categoryId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
        } catch {
            // The caller renders an empty selector and preserves the current value.
        }
        return [];
    };

    const loadProductDetails = async (accountId, rowIndex) => {
        if (!accountId) return null;
        try {
            const response = await apiClient.get(`/HomeApi/product-details/${accountId}`);
            if (response.data.success && response.data.data) {
                const product = response.data.data;
                const accountGroup = Number(product.accountGroupId);

                setEntries(prev => {
                    const updated = [...prev];
                    if (updated[rowIndex]) {
                        const hasExistingRate = updated[rowIndex].rate != null && updated[rowIndex].rate !== 0;
                        const voucherWillContainExpense =
                            isAccountGroup(accountGroup, ACCOUNT_GROUP.EXPENSE) ||
                            prev.some((entry, index) =>
                                index !== rowIndex &&
                                isAccountGroup(entry.accountGroup, ACCOUNT_GROUP.EXPENSE)
                            );

                        updated[rowIndex] = {
                            ...updated[rowIndex],
                            accountGroup,
                            accountType: product.accountTypeName || null,
                            dataPkg: product.pkg || null,
                            pkg: updated[rowIndex].pkg || product.pkg || null
                        };

                        if (voucherWillContainExpense) {
                            updated[rowIndex] = clearProductMechanics(updated[rowIndex]);
                        } else if (product.saleRate !== null && product.saleRate !== undefined) {
                            if (!isEditMode || !hasExistingRate) {
                                updated[rowIndex].rate = parseFloat(parseFloat(product.saleRate).toFixed(4));
                            }
                        }
                    }
                    return updated;
                });

                return product;
            }
        } catch {
            // Account metadata errors are surfaced by server validation on save.
        }
        return null;
    };

    const handleProjectChange = async (projectId) => {
        setSelectedProjectId(projectId);
        if (projectId) {
            localStorage.setItem(LAST_PROJECT_STORAGE_KEY, projectId.toString());
        }
        setEntries(prev => prev.map(entry => ({
            ...entry,
            categoryId: null,
            accountId: null,
            accountGroup: null,
            accountType: null
        })));

        if (projectId) {
            const categories = await loadCategories(projectId, 0);
            const categoriesMap = {};
            entries.forEach((_, index) => {
                categoriesMap[index] = categories;
            });
            setRowCategories(categoriesMap);
            setRowAccounts({});
        } else {
            setRowCategories({});
            setRowAccounts({});
        }
    };

    const handleCategoryChange = async (rowIndex, categoryId) => {
        setEntries(prev => {
            const updated = [...prev];
            updated[rowIndex] = {
                ...updated[rowIndex],
                categoryId,
                accountId: null,
                accountGroup: null,
                accountType: null
            };
            return updated;
        });

        if (categoryId && selectedProjectId) {
            const accounts = await loadAccounts(categoryId, rowIndex);
            setRowAccounts(prev => ({
                ...prev,
                [rowIndex]: accounts
            }));
        } else {
            setRowAccounts(prev => {
                const updated = { ...prev };
                delete updated[rowIndex];
                return updated;
            });
        }
    };

    const handleAccountChange = async (rowIndex, accountId) => {
        setEntries(prev => {
            const entry = prev[rowIndex];
            const hasDebit = entry && entry.debit && entry.debit > 0;
            const hasCredit = entry && entry.credit && entry.credit > 0;
            const shouldRecalculate = hasDebit || hasCredit;

            if (shouldRecalculate) {
                entry._needsRecalculation = hasDebit ? 'debit' : 'credit';
            }

            const updated = [...prev];
            updated[rowIndex] = {
                ...updated[rowIndex],
                accountId,
                rate: null,
                rbp: 'Y',
                pkg: 0,
                dataPkg: null,
                _needsRecalculation: shouldRecalculate ? (hasDebit ? 'debit' : 'credit') : null
            };
            return updated;
        });

        if (accountId) {
            await loadProductDetails(accountId, rowIndex);
            setTimeout(() => {
                calculateRowAmounts(rowIndex, true);
            }, 100);
        }
    };

    const calculateRowAmounts = (rowIndex, forceRecalculate = false) => {
        setEntries(prev => {
            const updated = [...prev];
            const entry = updated[rowIndex];
            if (!entry) return prev;
            if (prev.some(item => isAccountGroup(item.accountGroup, ACCOUNT_GROUP.EXPENSE))) {
                updated[rowIndex] = clearProductMechanics(entry);
                return updated;
            }

            const rbp = entry.rbp === 'Y';
            const qty = parseFloat(entry.qty) || 0;
            const rate = parseFloat(entry.rate) || 0;
            const pkg = parseFloat(entry.pkg || entry.dataPkg) || 0;

            let totalAmount = 0;
            if (rbp && pkg > 0) {
                totalAmount = pkg * qty * rate;
            } else {
                totalAmount = qty * rate;
            }

            const percent = parseFloat(entry.percent) || 0;
            const discount = parseFloat(((totalAmount * percent) / 100).toFixed(2));
            const netAmount = totalAmount - discount;

            const hasDebit = entry.debit && entry.debit > 0;
            const hasCredit = entry.credit && entry.credit > 0;
            const recalculationField = entry._needsRecalculation;

            updated[rowIndex] = {
                ...entry,
                disc: discount,
                totalAmount
            };

            delete updated[rowIndex]._needsRecalculation;

            if (forceRecalculate && recalculationField && netAmount > 0) {
                if (recalculationField === 'debit') {
                    updated[rowIndex].debit = parseFloat(netAmount.toFixed(2));
                    updated[rowIndex].credit = null;
                } else {
                    updated[rowIndex].credit = parseFloat(netAmount.toFixed(2));
                    updated[rowIndex].debit = null;
                }
            } else if (hasDebit && netAmount > 0) {
                updated[rowIndex].debit = parseFloat(netAmount.toFixed(2));
                updated[rowIndex].credit = null;
            } else if (hasCredit && netAmount > 0) {
                updated[rowIndex].credit = parseFloat(netAmount.toFixed(2));
                updated[rowIndex].debit = null;
            } else if (!hasDebit && !hasCredit && netAmount > 0) {
                updated[rowIndex].debit = parseFloat(netAmount.toFixed(2));
                updated[rowIndex].credit = null;
            }

            return updated;
        });
        updateTotals();
    };

    const updateTotals = () => {
    };

    const addNewRow = useCallback(async () => {
        document.dispatchEvent(new MouseEvent('mousedown', {
            bubbles: true,
            cancelable: true,
            view: window
        }));

        const newIndex = entries.length;
        setEntries(prev => {
            const blankEntry = {
            entryId: null,
            categoryId: null,
            accountId: null,
            pkg: 0,
            rbp: 'Y',
            des: '',
            qty: null,
            rate: null,
            percent: 0,
            disc: 0,
            debit: null,
            credit: null,
            accountGroup: null,
            accountType: null,
            dataPkg: null,
            totalAmount: 0
            };

            const voucherHasExpense = prev.some(
                entry => Number(entry.accountGroup) === ACCOUNT_GROUP.EXPENSE
            );

            return [
                ...prev,
                voucherHasExpense ? clearProductMechanics(blankEntry) : blankEntry
            ];
        });

        if (selectedProjectId) {
            const categories = await loadCategories(selectedProjectId, newIndex);
            setRowCategories(prev => ({
                ...prev,
                [newIndex]: categories
            }));
        }

        setTimeout(() => {
            const table = document.querySelector('table tbody');
            if (table) {
                const rows = table.querySelectorAll('tr');
                if (rows[newIndex]) {
                    rows[newIndex].scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                    const firstTd = rows[newIndex].querySelector('td:first-child');
                    if (firstTd) {
                        const categoryInput = firstTd.querySelector('input.select-search');
                        if (categoryInput) {
                            setTimeout(() => {
                                categoryInput.focus();
                                categoryInput.dispatchEvent(new Event('focus', { bubbles: true }));
                            }, 50);
                        }
                    }
                }
            }
        }, 100);
    }, [selectedProjectId, entries.length]);

    useEffect(() => {
        const handleKeyDown = (e) => {
            if (e.altKey && e.key.toLowerCase() === 'n') {
                e.preventDefault();
                addNewRow();
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => {
            window.removeEventListener('keydown', handleKeyDown);
        };
    }, [addNewRow]);

    const removeRow = (index) => {
        if (entries.length > 1) {
            setEntries(prev => prev.filter((_, i) => i !== index));

            setRowCategories(prev => {
                const updated = {};
                Object.keys(prev).forEach(key => {
                    const oldIndex = parseInt(key);
                    if (oldIndex < index) {
                        updated[oldIndex] = prev[oldIndex];
                    } else if (oldIndex > index) {
                        updated[oldIndex - 1] = prev[oldIndex];
                    }
                });
                return updated;
            });

            setRowAccounts(prev => {
                const updated = {};
                Object.keys(prev).forEach(key => {
                    const oldIndex = parseInt(key);
                    if (oldIndex < index) {
                        updated[oldIndex] = prev[oldIndex];
                    } else if (oldIndex > index) {
                        updated[oldIndex - 1] = prev[oldIndex];
                    }
                });
                return updated;
            });

            updateTotals();
        } else {
            setError('Cannot delete the last row. At least one entry is required.');
        }
    };

    const validateForm = () => {
        const errors = [];

        if (isBookerRequired && (!bookerId || bookerId <= 0)) {
            errors.push('A valid Booker must be selected.');
        } else if (bookerId != null && bookerId <= 0) {
            errors.push('The selected Booker is invalid.');
        }

        if (!selectedProjectId || selectedProjectId <= 0) {
            errors.push('Project is required.');
        }

        if (entries.length === 0) {
            errors.push('At least one entry is required.');
            return errors;
        }

        if ((!isEditMode || isApprovalEditMode || isEditingManualExpenseVoucher) && entries.length < 2) {
            errors.push('A journal voucher must contain at least two entries.');
        }

        let totalDebit = 0;
        let totalCredit = 0;

        entries.forEach((entry, index) => {
            const prefix = `Entry ${index + 1}: `;

            if (!entry.categoryId || entry.categoryId <= 0) {
                errors.push(`${prefix}Category is required.`);
            }
            if (!entry.accountId || entry.accountId <= 0) {
                errors.push(`${prefix}Account is required.`);
            }

            if (!entry.categoryId || !entry.accountId) return;

            const debit = parseFloat(entry.debit) || 0;
            const credit = parseFloat(entry.credit) || 0;

            if (debit < 0 || credit < 0) {
                errors.push(`${prefix}Debit and credit values cannot be negative.`);
            } else if (debit <= 0 && credit <= 0) {
                errors.push(`${prefix}Either Debit or Credit must be filled.`);
            } else if (debit > 0 && credit > 0) {
                errors.push(`${prefix}Only one of Debit or Credit can be filled, not both.`);
            }

            totalDebit += debit;
            totalCredit += credit;

            if (!hasExpenseAccount && isAccountGroup(entry.accountGroup, ACCOUNT_GROUP.PRODUCT)) {
                const rate = parseFloat(entry.rate) || 0;
                if (isApprovalEditMode ? rate <= 0 : rate < 0) {
                    errors.push(
                        `${prefix}Rate must be ${
                            isApprovalEditMode ? 'greater than zero' : 'greater than or equal to zero'
                        } for product accounts.`
                    );
                }
                if (requireQuantity || isApprovalEditMode) {
                    const qty = parseFloat(entry.qty) || 0;
                    if (qty <= 0) {
                        errors.push(`${prefix}Quantity must be greater than zero for product accounts.`);
                    }
                }
                if (!entry.rbp || (entry.rbp !== 'Y' && entry.rbp !== 'N')) {
                    errors.push(`${prefix}RBP must be Y or N for product accounts.`);
                }
            }
        });

        if (saveAsRecipe && !recipeName.trim()) {
            errors.push('Recipe name is required when saving as a recipe.');
        }

        if (Math.abs(totalDebit - totalCredit) > 0.01) {
            const difference = Math.abs(totalDebit - totalCredit).toFixed(2);
            errors.push(
                `Debits (${totalDebit.toFixed(2)}) and credits (${totalCredit.toFixed(2)}) must be equal. ` +
                `Difference: ${difference}`
            );
        }

        return errors;
    };

    const handleSubmit = async (e, action = 'submit') => {
        e.preventDefault();

        if (isSubmitting || loading || isSubmittingRef.current) {
            return;
        }

        if (isVoucherEditMode && isEditingManualExpenseVoucher && !hasExpenseAccount) {
            const message =
                'This Expense voucher must retain at least one Expense account. ' +
                'Restore an Expense line, or cancel this edit and delete the voucher through its authorized workflow.';
            setValidationErrors([message]);
            setShowExpenseAccountRequiredModal(true);
            return;
        }

        isSubmittingRef.current = true;
        setIsSubmitting(true);
        setError('');
        setSuccess('');
        setValidationErrors([]);

        const errors = validateForm();
        if (errors.length > 0) {
            setValidationErrors(errors);
            isSubmittingRef.current = false;
            setIsSubmitting(false);
            return;
        }

        setLoading(true);

        try {
            const model = {
                projectId: selectedProjectId,
                voucherVersion: isVoucherEditMode ? voucherVersion : null,
                entryDate: entryDate,
                dueDate: dueDate,
                creditDays: creditDays !== '' && creditDays !== null ? (parseInt(creditDays) || 0) : 0,
                bookerId: bookerId,
                requireQuantity: hasExpenseAccount ? false : requireQuantity,
                isBaseVoucher: hasExpenseAccount ? false : isBaseVoucher,
                saveAsRecipe: hasExpenseAccount ? false : saveAsRecipe,
                recipeName: !hasExpenseAccount && saveAsRecipe ? recipeName : null,
                entries: entries.map(entry => ({
                    entryId: entry.entryId,
                    categoryId: entry.categoryId,
                    accountId: entry.accountId,
                    pkg: hasExpenseAccount ? null : entry.pkg,
                    rbp: hasExpenseAccount ? null : entry.rbp,
                    des: entry.des || '',
                    qty: hasExpenseAccount ? null : (entry.qty || 0),
                    rate: hasExpenseAccount ? null : (entry.rate || 0),
                    percent: hasExpenseAccount ? null : (entry.percent || 0),
                    disc: hasExpenseAccount ? null : (entry.disc || 0),
                    debit: entry.debit || 0,
                    credit: entry.credit || 0
                }))
            };

            if (isApprovalEditMode) {
                const response = await approvalApi.update(editingApprovalId, model);
                const result = readPayload(response);
                const status = String(result.status || '').toLowerCase();
                const successMessage = response.data?.message || (
                    status === 'postednormally'
                        ? `The final Expense account was removed and the voucher was posted normally${
                            result.journalVoucherId ? ` as JV-${result.journalVoucherId}` : ''
                        }.`
                        : 'Pending voucher updated successfully.'
                );

                setSuccess(successMessage);
                setLoading(false);
                isSubmittingRef.current = false;
                setIsSubmitting(false);

                setTimeout(() => {
                    navigate('/approvals/pending', {
                        replace: true,
                        state: { approvalMessage: successMessage }
                    });
                }, 800);
                return;
            }

            const params = new URLSearchParams();
            if (action === 'submit_print') {
                params.append('action', 'submit_print');
            }
            if (isVoucherEditMode) {
                params.append('editingVoucherId', editingVoucherId);
            }

            const response = await apiClient.post(`/HomeApi/entry?${params.toString()}`, model);

            if (response.data.success) {
                const isPendingApproval =
                    response.data.isPending ?? ['pending', 'pendingapproval'].includes(
                        String(response.data.status || '').toLowerCase()
                    );
                const successMessage = response.data.message || (isPendingApproval
                    ? 'Voucher submitted for approval'
                    : (isVoucherEditMode ? 'Journal voucher updated successfully' : 'Journal entry created successfully'));
                setSuccess(successMessage);
                localStorage.setItem(LAST_JV_MESSAGE_KEY, successMessage);

                if (!isPendingApproval && response.data.shouldPrint && response.data.voucherId) {
                    const printUrl = `/reporting/print-voucher?voucherId=${response.data.voucherId}&type=JV`;
                    window.open(printUrl, '_blank');
                }

                const projectIdFromResponse = response.data.projectId || model.projectId;
                if (projectIdFromResponse) {
                    localStorage.setItem(LAST_PROJECT_STORAGE_KEY, projectIdFromResponse.toString());
                    setSelectedProjectId(projectIdFromResponse);
                }

                setLoading(false);
                isSubmittingRef.current = false;
                setIsSubmitting(false);

                setTimeout(() => {
                    if (isVoucherEditMode) {
                        window.location.href = '/home/entry';
                    } else {
                        setEntries([{
                            entryId: null,
                            categoryId: null,
                            accountId: null,
                            pkg: null,
                            rbp: 'Y',
                            des: '',
                            qty: null,
                            rate: null,
                            percent: 0,
                            disc: 0,
                            debit: null,
                            credit: null,
                            accountGroup: null,
                            accountType: null,
                            dataPkg: null,
                            totalAmount: 0
                        }]);
                        setBookerId(bookers.length > 0 ? bookers[0].id : null);
                        setEntryDate(todayString());
                        setCreditDays(0);
                        setSuccess('');
                    }
                }, 2000);
            } else {
                setError(response.data.message || 'Failed to save entries');
                setLoading(false);
                isSubmittingRef.current = false;
                setIsSubmitting(false);
            }
        } catch (error) {
            setError(error.response?.data?.message || 'Failed to save entries');
            setLoading(false);
            isSubmittingRef.current = false;
            setIsSubmitting(false);
        }
    };  

    const updateMoneyInput = (rowIndex, field, rawValue) => {
        const fieldKey = `${field}-${rowIndex}`;
        const formattedValue = formatMoneyInput(rawValue);
        const numericValue = parseMoneyInput(formattedValue);

        setMoneyInputValues(prev => ({ ...prev, [fieldKey]: formattedValue }));
        setEntries(prev => {
            const updated = [...prev];
            updated[rowIndex] = { ...updated[rowIndex], [field]: numericValue };
            if (numericValue) {
                updated[rowIndex][field === 'debit' ? 'credit' : 'debit'] = null;
            }
            return updated;
        });
        updateTotals();
    };

    const beginMoneyInput = (rowIndex, field, value) => {
        const fieldKey = `${field}-${rowIndex}`;
        setFocusedFields(prev => ({ ...prev, [fieldKey]: true }));
        setMoneyInputValues(prev => ({
            ...prev,
            [fieldKey]: formatMoneyInput(value),
        }));
    };

    const finishMoneyInput = (rowIndex, field, rawValue) => {
        const fieldKey = `${field}-${rowIndex}`;
        const numericValue = parseMoneyInput(rawValue);

        setFocusedFields(prev => {
            const updated = { ...prev };
            delete updated[fieldKey];
            return updated;
        });
        setMoneyInputValues(prev => {
            const updated = { ...prev };
            delete updated[fieldKey];
            return updated;
        });
        setEntries(prev => {
            const updated = [...prev];
            updated[rowIndex] = {
                ...updated[rowIndex],
                [field]: numericValue === null ? null : Number(numericValue.toFixed(2)),
            };
            return updated;
        });
        updateTotals();
    };

    const getMoneyInputValue = (rowIndex, field, value) => {
        const fieldKey = `${field}-${rowIndex}`;
        if (!focusedFields[fieldKey]) return formatMoney(value);

        const editingValue = moneyInputValues[fieldKey];
        return editingValue === '' && value !== null && value !== undefined
            ? formatMoneyInput(value)
            : (editingValue ?? formatMoneyInput(value));
    };

    const totalDebit = entries.reduce((sum, entry) => sum + (parseFloat(entry.debit) || 0), 0);
    const totalCredit = entries.reduce((sum, entry) => sum + (parseFloat(entry.credit) || 0), 0);
    const totalBalance = totalDebit - totalCredit;

    const accountEntriesCount = entries.filter(e => isAccountGroup(e.accountGroup, ACCOUNT_GROUP.ACCOUNT)).length;
    const productEntriesCount = entries.filter(e => isAccountGroup(e.accountGroup, ACCOUNT_GROUP.PRODUCT)).length;
    const showBaseVoucherCheckbox = !hasExpenseAccount && accountEntriesCount === 1 && productEntriesCount >= 1;

    const allEntriesAreProducts = entries.every(e =>
        !e.accountGroup || isAccountGroup(e.accountGroup, ACCOUNT_GROUP.PRODUCT)
    );
    const hasAtLeastOneProduct = productEntriesCount > 0;
    const showSaveRecipeCheckbox =
        !hasExpenseAccount && entries.length >= 2 && allEntriesAreProducts && hasAtLeastOneProduct;

    return (
        <div className="container-fluid pt-2">
            {isApprovalEditMode && (
                <div className="d-flex justify-content-end align-items-center mb-3">
                    <Button
                        variant="outline-secondary"
                        onClick={() => navigate('/approvals/pending')}
                        disabled={loading || isSubmitting}
                    >
                        <i className="fas fa-arrow-left me-1"></i>
                        Pending Approvals
                    </Button>
                </div>
            )}

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            {success && (
                <Alert variant="success" dismissible onClose={() => {
                    setSuccess('');
                    localStorage.removeItem(LAST_JV_MESSAGE_KEY);
                }}>
                    <i className="fas fa-check-circle me-2"></i>{success}
                </Alert>
            )}

            {bookersLoaded && !hasBookers && isBookerRequired && (
                <Alert variant="warning" dismissible>
                    <i className="fas fa-exclamation-triangle me-2"></i>
                    <strong>No Bookers Available:</strong> Please add at least one booker before creating journal entries.
                </Alert>
            )}

            {validationErrors.length > 0 && (
                <Alert variant="danger" dismissible onClose={() => setValidationErrors([])}>
                    <i className="fas fa-exclamation-triangle me-2"></i>
                    <ul className="mb-0">
                        {validationErrors.map((err, idx) => (
                            <li key={idx}>{err}</li>
                        ))}
                    </ul>
                </Alert>
            )}

            {isApprovalEditMode && (
                <Alert variant="warning">
                    <i className="fas fa-lock me-2"></i>
                    You are editing the complete pending voucher snapshot. Saving keeps it pending;
                    it will not receive a JV number or enter reports until it is approved.
                </Alert>
            )}

            {isVoucherEditMode && isEditingManualExpenseVoucher && (
                <Alert variant="info">
                    <i className="fas fa-receipt me-2"></i>
                    This is an Expense voucher. Edit it as one complete balanced table; at least one
                    Expense account must remain before it can be submitted for reapproval.
                </Alert>
            )}

            <form onSubmit={(e) => handleSubmit(e, 'submit')}>
                <Card className="shadow mb-3">
                    <Card.Header className="py-2">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-info-circle me-2"></i>Entry Details
                        </h6>
                    </Card.Header>
                    <Card.Body className="py-2">
                        <div className="row mb-2">
                            <div className="col-md-6 mb-2 mb-md-0">
                                <Form.Label>Entry Date:</Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text">
                                        <i className="fas fa-calendar-alt"></i>
                                    </span>
                                    <Form.Control
                                        type="date"
                                        value={entryDate}
                                        onChange={(e) => setEntryDate(e.target.value)}
                                        disabled={isApprovalEditMode}
                                        required
                                    />
                                </div>
                            </div>
                            <div className="col-md-6">
                                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text"><i className="fas fa-project-diagram"></i></span>
                                    <SearchableSelect
                                        value={selectedProjectId}
                                        onChange={(value) => handleProjectChange(value ? parseInt(value) : null)}
                                        options={projects.map(p => ({ id: p.id, name: p.name }))}
                                        placeholder="Select Project"
                                        required
                                        disabled={isApprovalEditMode}
                                    />
                                </div>
                            </div>
                        </div>
                    </Card.Body>
                </Card>

                <Card className="shadow mb-3">
                    <Card.Header className="py-2 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-list-alt me-2"></i>Journal Entries
                        </h6>
                        <Button variant="primary" size="sm" type="button" onClick={addNewRow}>
                            <i className="fas fa-plus"></i> Add Row (Alt+N)
                        </Button>
                    </Card.Header>
                    <Card.Body className="p-0">
                        <div className="table-responsive">
                            <Table bordered className="align-middle mb-0">
                                <thead className="table-primary">
                                    <tr>
                                        <th className="text-center">Category <span className="text-danger">*</span></th>
                                        <th className="text-center">Account <span className="text-danger">*</span></th>
                                        <th className="text-center">PKG</th>
                                        <th className="text-center">RBP</th>
                                        <th className="text-center">Description</th>
                                        <th className="text-center">Qty <span className="text-danger" style={{ display: requireQuantity ? 'inline' : 'none' }}>*</span></th>
                                        <th className="text-center">Rate</th>
                                        <th className="text-center">%</th>
                                        <th className="text-center">Discount</th>
                                        <th className="text-center">Debit</th>
                                        <th className="text-center">Credit</th>
                                        <th className="text-center"></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries.map((entry, index) => {
                                        const isProductAccount =
                                            isAccountGroup(entry.accountGroup, ACCOUNT_GROUP.PRODUCT);
                                        const productMechanicsDisabled =
                                            hasExpenseAccount || !isProductAccount;

                                        return (
                                            <tr key={index}>
                                                <td>
                                                    <SearchableSelect
                                                        value={entry.categoryId}
                                                        onChange={(value) => handleCategoryChange(index, value ? parseInt(value) : null)}
                                                        options={(rowCategories[index] || []).map(c => ({ id: c.id, name: c.name }))}
                                                        placeholder="Search category..."
                                                        size="sm"
                                                        required
                                                        disabled={!selectedProjectId}
                                                        onClear={() => {
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index] = {
                                                                    ...updated[index],
                                                                    categoryId: null,
                                                                    accountId: null,
                                                                    accountGroup: null,
                                                                    accountType: null,
                                                                    pkg: 0,
                                                                    dataPkg: null
                                                                };
                                                                return updated;
                                                            });
                                                            setRowAccounts(prev => {
                                                                const updated = { ...prev };
                                                                delete updated[index];
                                                                return updated;
                                                            });
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <SearchableSelect
                                                        value={entry.accountId}
                                                        onChange={(value) => handleAccountChange(index, value ? parseInt(value) : null)}
                                                        options={(rowAccounts[index] || []).map(a => ({ id: a.id, name: a.name }))}
                                                        placeholder="Search account..."
                                                        size="sm"
                                                        required
                                                        disabled={!entry.categoryId}
                                                        onClear={() => {
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index] = {
                                                                    ...updated[index],
                                                                    accountId: null,
                                                                    accountGroup: null,
                                                                    accountType: null
                                                                };
                                                                return updated;
                                                            });
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        min="0"
                                                        step="1"
                                                        value={entry.pkg ?? ''}
                                                        readOnly
                                                        disabled
                                                        className="bg-light"
                                                        tabIndex="-1"
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Select
                                                        size="sm"
                                                        className="rbp-select-no-arrow"
                                                        value={entry.rbp || ''}
                                                        onChange={(e) => {
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index].rbp = e.target.value;
                                                                return updated;
                                                            });
                                                            calculateRowAmounts(index);
                                                        }}
                                                        disabled={productMechanicsDisabled}
                                                    >
                                                        <option value="">—</option>
                                                        <option value="Y">Y</option>
                                                        <option value="N">N</option>
                                                    </Form.Select>
                                                </td>
                                                <td>
                                                    <div 
                                                        className="desc-input-wrapper"
                                                        onMouseEnter={() => {
                                                            if (entry.des && entry.des.trim()) {
                                                                setHoveredDescIndex(index);
                                                            }
                                                        }}
                                                        onMouseLeave={() => {
                                                            setHoveredDescIndex(null);
                                                        }}
                                                    >
                                                        <Form.Control
                                                            size="sm"
                                                            type="text"
                                                            value={entry.des || ''}
                                                            onChange={(e) => {
                                                                setEntries(prev => {
                                                                    const updated = [...prev];
                                                                    updated[index].des = e.target.value;
                                                                    return updated;
                                                                });
                                                            }}
                                                            placeholder="Description"
                                                        />
                                                        {hoveredDescIndex === index && entry.des && entry.des.trim() && (
                                                            <div className="desc-tooltip-3d">
                                                                {entry.des}
                                                            </div>
                                                        )}
                                                    </div>
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        className="no-arrow"
                                                        step="1"
                                                        value={entry.qty || ''}
                                                        onChange={(e) => {
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index].qty = e.target.value ? parseInt(e.target.value) : null;
                                                                return updated;
                                                            });
                                                            calculateRowAmounts(index);
                                                        }}
                                                        disabled={productMechanicsDisabled}
                                                        required={!hasExpenseAccount && isProductAccount && requireQuantity}
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        className="no-arrow"
                                                        min="0"
                                                        step="0.0001"
                                                        value={focusedFields[`rate-${index}`] ? (entry.rate != null ? entry.rate : '') : (entry.rate != null ? parseFloat(entry.rate).toFixed(4) : '')}
                                                        onChange={(e) => {
                                                            const inputValue = e.target.value;
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index].rate = inputValue === '' ? null : (isNaN(parseFloat(inputValue)) ? prev[index].rate : parseFloat(inputValue));
                                                                return updated;
                                                            });
                                                            calculateRowAmounts(index);
                                                        }}
                                                        onFocus={() => {
                                                            setFocusedFields(prev => ({ ...prev, [`rate-${index}`]: true }));
                                                        }}
                                                        disabled={productMechanicsDisabled}
                                                        onBlur={(e) => {
                                                            setFocusedFields(prev => {
                                                                const newFields = { ...prev };
                                                                delete newFields[`rate-${index}`];
                                                                return newFields;
                                                            });
                                                            if (e.target.value) {
                                                                setEntries(prev => {
                                                                    const updated = [...prev];
                                                                    updated[index].rate = parseFloat(parseFloat(e.target.value).toFixed(4));
                                                                    return updated;
                                                                });
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        className="no-arrow"
                                                        min="0"
                                                        max="100"
                                                        step="0.01"
                                                        value={focusedFields[`percent-${index}`] ? (entry.percent != null ? entry.percent : '') : (entry.percent != null ? parseFloat(entry.percent).toFixed(2) : '')}
                                                        onChange={(e) => {
                                                            const inputValue = e.target.value;
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index].percent = inputValue === '' ? 0 : (isNaN(parseFloat(inputValue)) ? prev[index].percent : parseFloat(inputValue));
                                                                return updated;
                                                            });
                                                            calculateRowAmounts(index);
                                                        }}
                                                        onFocus={() => {
                                                            setFocusedFields(prev => ({ ...prev, [`percent-${index}`]: true }));
                                                        }}
                                                        disabled={productMechanicsDisabled}
                                                        onBlur={(e) => {
                                                            setFocusedFields(prev => {
                                                                const newFields = { ...prev };
                                                                delete newFields[`percent-${index}`];
                                                                return newFields;
                                                            });
                                                            if (e.target.value) {
                                                                setEntries(prev => {
                                                                    const updated = [...prev];
                                                                    updated[index].percent = parseFloat(parseFloat(e.target.value).toFixed(2));
                                                                    return updated;
                                                                });
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        className="no-arrow bg-light"
                                                        min="0"
                                                        step="0.0001"
                                                        value={entry.disc == null ? '' : Number(entry.disc).toFixed(2)}
                                                        readOnly
                                                        tabIndex="-1"
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="text"
                                                        inputMode="decimal"
                                                        data-money-field="debit"
                                                        aria-label={`Entry ${index + 1} debit`}
                                                        className="no-arrow"
                                                        value={getMoneyInputValue(index, 'debit', entry.debit)}
                                                        onChange={(e) => updateMoneyInput(index, 'debit', e.target.value)}
                                                        onFocus={() => {
                                                            beginMoneyInput(index, 'debit', entry.debit);
                                                            if (!entry.debit && !entry.credit) {
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                        onBlur={(e) => finishMoneyInput(index, 'debit', e.target.value)}
                                                        onKeyDown={(e) => {
                                                            if (e.key === 'Tab' && !e.shiftKey) {
                                                                const inputValue = e.target.value;
                                                                const isEmpty = parseMoneyInput(inputValue) === null || parseMoneyInput(inputValue) === 0;

                                                                if (isEmpty && (!entry.credit || entry.credit === 0)) {
                                                                    const qty = parseFloat(entry.qty) || 0;
                                                                    const rate = parseFloat(entry.rate) || 0;
                                                                    const rbp = entry.rbp === 'Y';
                                                                    const pkg = parseFloat(entry.pkg || entry.dataPkg) || 0;
                                                                    const percent = parseFloat(entry.percent) || 0;

                                                                    let totalAmount = 0;
                                                                    if (rbp && pkg > 0) {
                                                                        totalAmount = pkg * qty * rate;
                                                                    } else {
                                                                        totalAmount = qty * rate;
                                                                    }

                                                                    const discount = (totalAmount * percent) / 100;
                                                                    const netAmount = totalAmount - discount;

                                                                    if (netAmount > 0) {
                                                                        e.preventDefault();
                                                                        setEntries(prev => {
                                                                            const updated = [...prev];
                                                                            updated[index].credit = parseFloat(netAmount.toFixed(2));
                                                                            updated[index].debit = null;
                                                                            return updated;
                                                                        });
                                                                        updateTotals();
                                                                        setTimeout(() => {
                                                                            const row = e.target.closest('tr');
                                                                            if (row) {
                                                                            const creditInput = row.querySelector('input[data-money-field="credit"]');
                                                                                if (creditInput) {
                                                                                    creditInput.focus();
                                                                                    creditInput.select();
                                                                                }
                                                                            }
                                                                        }, 10);
                                                                    }
                                                                }
                                                            }
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="text"
                                                        inputMode="decimal"
                                                        data-money-field="credit"
                                                        aria-label={`Entry ${index + 1} credit`}
                                                        className="no-arrow"
                                                        value={getMoneyInputValue(index, 'credit', entry.credit)}
                                                        onChange={(e) => updateMoneyInput(index, 'credit', e.target.value)}
                                                        onFocus={() => {
                                                            beginMoneyInput(index, 'credit', entry.credit);
                                                            if (!entry.debit && !entry.credit) {
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                        onBlur={(e) => finishMoneyInput(index, 'credit', e.target.value)}
                                                        onKeyDown={(e) => {
                                                            if (e.key === 'Tab' && !e.shiftKey) {
                                                                const inputValue = e.target.value;
                                                                const isEmpty = parseMoneyInput(inputValue) === null || parseMoneyInput(inputValue) === 0;

                                                                if (isEmpty && (!entry.debit || entry.debit === 0)) {
                                                                    const qty = parseFloat(entry.qty) || 0;
                                                                    const rate = parseFloat(entry.rate) || 0;
                                                                    const rbp = entry.rbp === 'Y';
                                                                    const pkg = parseFloat(entry.pkg || entry.dataPkg) || 0;
                                                                    const percent = parseFloat(entry.percent) || 0;

                                                                    let totalAmount = 0;
                                                                    if (rbp && pkg > 0) {
                                                                        totalAmount = pkg * qty * rate;
                                                                    } else {
                                                                        totalAmount = qty * rate;
                                                                    }

                                                                    const discount = (totalAmount * percent) / 100;
                                                                    const netAmount = totalAmount - discount;

                                                                    if (netAmount > 0) {
                                                                        e.preventDefault();
                                                                        setEntries(prev => {
                                                                            const updated = [...prev];
                                                                            updated[index].debit = parseFloat(netAmount.toFixed(2));
                                                                            updated[index].credit = null;
                                                                            return updated;
                                                                        });
                                                                        updateTotals();
                                                                    }
                                                                }
                                                            }
                                                        }}
                                                    />
                                                </td>
                                                <td>
                                                    <Button
                                                        variant="danger"
                                                        size="sm"
                                                        type="button"
                                                        onClick={() => removeRow(index)}
                                                        disabled={entries.length === 1}
                                                    >
                                                        <i className="fas fa-times"></i>
                                                    </Button>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot className="table-secondary">
                                    <tr>
                                        <td colSpan="9" className="text-end fw-bold">TOTAL</td>
                                        <td className="fw-bold">{formatMoney(totalDebit)}</td>
                                        <td className="fw-bold">{formatMoney(totalCredit)}</td>
                                        <td></td>
                                    </tr>
                                    <tr>
                                        <td colSpan="9" className="text-end fw-bold">BALANCE</td>
                                        <td colSpan="2" className="fw-bold text-center">{formatMoney(totalBalance)}</td>
                                        <td></td>
                                    </tr>
                                </tfoot>
                            </Table>
                        </div>
                    </Card.Body>
                </Card>

                <Card className="shadow mb-3">
                    <Card.Header className="py-2">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-cog me-2"></i>Additional Details
                        </h6>
                    </Card.Header>
                    <Card.Body className="py-2">
                        <div className="row">
                            <div className="col-md-4 mb-2">
                                <Form.Label>Credit Days</Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text"><i className="fas fa-calendar-day"></i></span>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        min="0"
                                        value={creditDays}
                                        onChange={(e) => setCreditDays(e.target.value === '' ? '' : parseInt(e.target.value) || '')}
                                    />
                                </div>
                            </div>
                            <div className="col-md-4 mb-2">
                                <Form.Label>Due Date</Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text">
                                        <i className="fas fa-calendar-check"></i>
                                    </span>
                                    <Form.Control
                                        type="date"
                                        value={dueDate}
                                        readOnly
                                        className="bg-light"
                                        tabIndex="-1"
                                    />
                                </div>
                            </div>
                            <div className="col-md-4 mb-2">
                                <Form.Label>
                                    Booker{isApprovalEditMode ? ' (Optional)' : ''}
                                </Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text"><i className="fas fa-user"></i></span>
                                    <SearchableSelect
                                        value={bookerId}
                                        onChange={(value) => setBookerId(value ? parseInt(value) : null)}
                                        options={hasBookers ? bookers.map(b => ({ id: b.id, name: b.name })) : []}
                                        placeholder={
                                            hasBookers
                                                ? (isApprovalEditMode ? "Select Booker (Optional)" : "Select Booker")
                                                : (isApprovalEditMode ? "No Booker selected" : "No bookers available")
                                        }
                                        disabled={!hasBookers}
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="row mt-3">
                            <div className="col-12">
                                <Form.Check
                                    type="checkbox"
                                    id="RequireQuantity"
                                    label={
                                        <>
                                            <span className="fw-bold text-info">
                                                <i className="fas fa-hashtag me-1"></i>Require Quantity for Product Accounts
                                            </span>
                                            <small className="text-muted ms-2">
                                                (When checked, quantity field will be mandatory for product entries)
                                            </small>
                                        </>
                                    }
                                    checked={requireQuantity}
                                    onChange={(e) => setRequireQuantity(e.target.checked)}
                                    disabled={hasExpenseAccount}
                                />
                            </div>
                        </div>

                        {showBaseVoucherCheckbox && (
                            <div className="row mt-2">
                                <div className="col-12">
                                    <Form.Check
                                        type="checkbox"
                                        id="IsBaseVoucher"
                                        label={
                                            <>
                                                <span className="fw-bold text-primary">
                                                    <i className="fas fa-layer-group me-1"></i>Mark as Base Voucher
                                                </span>
                                                <small className="text-muted ms-2">
                                                    (Base vouchers can be used to create multiple derived vouchers)
                                                </small>
                                            </>
                                        }
                                        checked={isBaseVoucher}
                                        onChange={(e) => setIsBaseVoucher(e.target.checked)}
                                    />
                                </div>
                            </div>
                        )}

                        {showSaveRecipeCheckbox && (
                            <div className="row mt-2">
                                <div className="col-12">
                                    <Form.Check
                                        type="checkbox"
                                        id="SaveAsRecipe"
                                        label={
                                            <>
                                                <span className="fw-bold text-success">
                                                    <i className="fas fa-bookmark me-1"></i>Save this recipe
                                                </span>
                                                <small className="text-muted ms-2">
                                                    (Available only for product entries with at least 2 items)
                                                </small>
                                            </>
                                        }
                                        checked={saveAsRecipe}
                                        onChange={(e) => setSaveAsRecipe(e.target.checked)}
                                    />
                                </div>
                            </div>
                        )}

                        {saveAsRecipe && (
                            <div className="row mt-3">
                                <div className="col-md-6">
                                    <Form.Label>Recipe Name <span className="text-danger">*</span></Form.Label>
                                    <div className="input-group">
                                        <span className="input-group-text"><i className="fas fa-tag"></i></span>
                                        <Form.Control
                                            type="text"
                                            value={recipeName}
                                            onChange={(e) => setRecipeName(e.target.value)}
                                            placeholder="Enter a name for this recipe"
                                            required={saveAsRecipe}
                                        />
                                    </div>
                                </div>
                            </div>
                        )}
                    </Card.Body>
                </Card>

                <div className="d-flex justify-content-between mb-3">
                    <Button
                        variant="secondary"
                        type="button"
                        onClick={() => isApprovalEditMode
                            ? navigate('/approvals/pending')
                            : navigate(-1)}
                        disabled={loading || isSubmitting}
                    >
                        <i className="fas fa-arrow-left me-1"></i> Back
                    </Button>
                    <div>
                        <Button
                            variant="primary"
                            type="button"
                            onClick={(e) => handleSubmit(e, 'submit')}
                            disabled={
                                loading ||
                                isSubmitting ||
                                (isBookerRequired && (!hasBookers || !bookerId))
                            }
                            className="me-2"
                        >
                            {loading ? (
                                <>
                                    <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                    {isApprovalEditMode
                                        ? 'Saving...'
                                        : (isVoucherEditMode ? 'Updating...' : 'Submitting...')}
                                </>
                            ) : (
                                <>
                                    <i className="fas fa-save me-1"></i>
                                    {isApprovalEditMode
                                        ? 'Save Pending Voucher'
                                        : (isVoucherEditMode ? 'Update Journal Voucher' : 'Submit Journal Entry')}
                                </>
                            )}
                        </Button>
                        {!isApprovalEditMode && (
                            <Button
                                variant="success"
                                type="button"
                                onClick={(e) => handleSubmit(e, 'submit_print')}
                                disabled={loading || isSubmitting || !hasBookers || !bookerId}
                            >
                                <i className="fas fa-print me-1"></i>
                                {isVoucherEditMode ? 'Update and Print' : 'Submit and Print'}
                            </Button>
                        )}
                    </div>
                </div>
            </form>

            <Modal
                show={showExpenseAccountRequiredModal}
                onHide={() => setShowExpenseAccountRequiredModal(false)}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Expense account required</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    This approved Expense voucher cannot be saved without an Expense account.
                    Restore at least one Expense line. If the voucher is no longer required, cancel
                    this edit and delete the voucher through its authorized workflow.
                </Modal.Body>
                <Modal.Footer>
                    <Button
                        variant="primary"
                        onClick={() => setShowExpenseAccountRequiredModal(false)}
                    >
                        Return to voucher
                    </Button>
                </Modal.Footer>
            </Modal>
        </div>
    );
};

export default HomeEntry;
