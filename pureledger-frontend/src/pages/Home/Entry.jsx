import { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Card, Form, Button, Table, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import './Entry.css';

const LAST_PROJECT_STORAGE_KEY = 'pl_last_selected_project';
const LAST_JV_MESSAGE_KEY = 'pl_last_jv_message';

const HomeEntry = () => {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const editingVoucherId = searchParams.get('voucherId') ? parseInt(searchParams.get('voucherId')) : null;
    const isEditMode = editingVoucherId != null;

    usePageTitle(isEditMode ? 'Edit Entry' : 'Entry');

    const isSubmittingRef = useRef(false);

    const [projects, setProjects] = useState([]);
    const [bookers, setBookers] = useState([]);
    const [selectedProjectId, setSelectedProjectId] = useState(null);
    const [entryDate, setEntryDate] = useState(new Date().toISOString().split('T')[0]);
    const [creditDays, setCreditDays] = useState(0);
    const [dueDate, setDueDate] = useState(new Date().toISOString().split('T')[0]);
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

    const persistedSuccessRef = useRef(null);
    const [focusedFields, setFocusedFields] = useState({});
    const [hoveredDescIndex, setHoveredDescIndex] = useState(null);

    useEffect(() => {
        loadEntryData();
        if (isEditMode) {
            loadVoucherForEdit();
        }
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
                entries.forEach((_, index) => {
                    categoriesMap[index] = categories;
                });
                setRowCategories(categoriesMap);
            };
            loadCategoriesForAllRows();
        }
    }, [selectedProjectId, entries.length]);

    useEffect(() => {
        if (entryDate) {
            const days = creditDays !== '' && creditDays !== null ? parseInt(creditDays) || 0 : 0;
            const date = new Date(entryDate);
            date.setDate(date.getDate() + days);
            setDueDate(date.toISOString().split('T')[0]);
        }
    }, [entryDate, creditDays]);


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
                const { model, projects: projData, bookers: bookData, selectedProjectId: projId } = response.data;
                setProjects(projData || []);
                setBookers(bookData || []);
                const bookersAvailable = (bookData || []).length > 0;
                setHasBookers(bookersAvailable);
                setBookersLoaded(true);
                setSelectedProjectId(projId);
                setEntryDate(model.entryDate || new Date().toISOString().split('T')[0]);
                setCreditDays(model.creditDays !== undefined && model.creditDays !== null ? model.creditDays : '');
                setDueDate(model.dueDate || new Date().toISOString().split('T')[0]);
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
                                    accountGroup = product.accountGroupId === 1 ? 'account' : 'product';
                                    accountType = product.accountTypeName || null;
                                    dataPkg = product.pkg || null;
                                }
                            } catch (err) {
                            }
                        }

                        const parsedQty = entry.qty != null ? (typeof entry.qty === 'number' ? entry.qty : parseFloat(entry.qty)) : null;
                        const parsedRate = entry.rate != null ? (typeof entry.rate === 'number' ? entry.rate : parseFloat(entry.rate)) : null;
                        const parsedPercent = entry.percent != null ? (typeof entry.percent === 'number' ? entry.percent : parseFloat(entry.percent)) : 0;
                        const parsedDisc = entry.disc != null ? (typeof entry.disc === 'number' ? entry.disc : parseFloat(entry.disc)) : 0;
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
        } catch (error) {
            setError('Failed to load voucher data');
        } finally {
            setLoading(false);
        }
    };

    const loadCategories = async (projectId, rowIndex) => {
        if (!projectId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
        } catch (error) {
        }
        return [];
    };

    const loadAccounts = async (categoryId, rowIndex) => {
        if (!categoryId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
        } catch (error) {
        }
        return [];
    };

    const loadProductDetails = async (accountId, rowIndex) => {
        if (!accountId) return null;
        try {
            const response = await apiClient.get(`/HomeApi/product-details/${accountId}`);
            if (response.data.success && response.data.data) {
                const product = response.data.data;
                const accountGroup = product.accountGroupId === 1 ? 'account' : 'product';

                setEntries(prev => {
                    const updated = [...prev];
                    if (updated[rowIndex]) {
                        const hasExistingRate = updated[rowIndex].rate != null && updated[rowIndex].rate !== 0;

                        updated[rowIndex] = {
                            ...updated[rowIndex],
                            accountGroup,
                            accountType: product.accountTypeName || null,
                            dataPkg: product.pkg || null,
                            pkg: updated[rowIndex].pkg || product.pkg || null
                        };

                        if (product.saleRate !== null && product.saleRate !== undefined) {
                            if (!isEditMode || !hasExistingRate) {
                                updated[rowIndex].rate = parseFloat(parseFloat(product.saleRate).toFixed(4));
                            }
                        }
                    }
                    return updated;
                });

                return product;
            }
        } catch (error) {
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
        setEntries(prev => [...prev, {
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

        if (!bookerId || bookerId <= 0) {
            errors.push('A valid Booker must be selected.');
        }

        if (!selectedProjectId || selectedProjectId <= 0) {
            errors.push('Project is required.');
        }

        if (entries.length === 0) {
            errors.push('At least one entry is required.');
            return errors;
        }

        if (entries.length < 2) {
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

            if (debit <= 0 && credit <= 0) {
                errors.push(`${prefix}Either Debit or Credit must be filled.`);
            } else if (debit > 0 && credit > 0) {
                errors.push(`${prefix}Only one of Debit or Credit can be filled, not both.`);
            }

            totalDebit += debit;
            totalCredit += credit;

            if (entry.accountGroup === 'product') {
                const rate = parseFloat(entry.rate) || 0;
                if (rate < 0) {
                    errors.push(`${prefix}Rate must be greater than or equal to zero for product accounts.`);
                }
                if (requireQuantity) {
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
                entryDate: entryDate,
                dueDate: dueDate,
                creditDays: creditDays !== '' && creditDays !== null ? (parseInt(creditDays) || 0) : 0,
                bookerId: bookerId,
                requireQuantity: requireQuantity,
                isBaseVoucher: isBaseVoucher,
                saveAsRecipe: saveAsRecipe,
                recipeName: saveAsRecipe ? recipeName : null,
                entries: entries.map(entry => ({
                    entryId: entry.entryId,
                    categoryId: entry.categoryId,
                    accountId: entry.accountId,
                    pkg: entry.pkg,
                    rbp: entry.rbp,
                    des: entry.des || '',
                    qty: entry.qty || 0,
                    rate: entry.rate || 0,
                    percent: entry.percent || 0,
                    disc: entry.disc || 0,
                    debit: entry.debit || 0,
                    credit: entry.credit || 0
                }))
            };

            const params = new URLSearchParams();
            if (action === 'submit_print') {
                params.append('action', 'submit_print');
            }
            if (isEditMode) {
                params.append('editingVoucherId', editingVoucherId);
            }

            const response = await apiClient.post(`/HomeApi/entry?${params.toString()}`, model);

            if (response.data.success) {
                const successMessage = response.data.message || (isEditMode ? 'Journal voucher updated successfully' : 'Journal entry created successfully');
                setSuccess(successMessage);
                localStorage.setItem(LAST_JV_MESSAGE_KEY, successMessage);

                if (response.data.shouldPrint && response.data.voucherId) {
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
                    if (isEditMode) {
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
                        setEntryDate(new Date().toISOString().split('T')[0]);
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

    const totalDebit = entries.reduce((sum, entry) => sum + (parseFloat(entry.debit) || 0), 0);
    const totalCredit = entries.reduce((sum, entry) => sum + (parseFloat(entry.credit) || 0), 0);
    const totalBalance = totalDebit - totalCredit;

    const accountEntriesCount = entries.filter(e => e.accountGroup === 'account').length;
    const productEntriesCount = entries.filter(e => e.accountGroup === 'product').length;
    const showBaseVoucherCheckbox = accountEntriesCount === 1 && productEntriesCount >= 1;

    const allEntriesAreProducts = entries.every(e => !e.accountGroup || e.accountGroup === 'product');
    const hasAtLeastOneProduct = productEntriesCount > 0;
    const showSaveRecipeCheckbox = entries.length >= 2 && allEntriesAreProducts && hasAtLeastOneProduct;

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2>
                    <i className={`fas fa-${isEditMode ? 'edit' : 'book-open'} me-2`}></i>
                    {isEditMode ? `Edit Journal Voucher #${editingVoucherId}` : 'Journal Entry'}
                </h2>
            </div>

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

            {bookersLoaded && !hasBookers && (
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
                                        const isProductAccount = entry.accountGroup === 'product';
                                        const isAccountAccount = entry.accountGroup === 'account';

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
                                                        value={entry.pkg ?? 0}
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
                                                        value={entry.rbp || 'Y'}
                                                        onChange={(e) => {
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                updated[index].rbp = e.target.value;
                                                                return updated;
                                                            });
                                                            calculateRowAmounts(index);
                                                        }}
                                                        disabled={isAccountAccount}
                                                    >
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
                                                        disabled={isAccountAccount}
                                                        required={isProductAccount && requireQuantity}
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
                                                        disabled={isAccountAccount}
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
                                                        disabled={isAccountAccount}
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
                                                        value={entry.disc ? entry.disc.toFixed(2) : '0.00'}
                                                        readOnly
                                                        tabIndex="-1"
                                                    />
                                                </td>
                                                <td>
                                                    <Form.Control
                                                        size="sm"
                                                        type="number"
                                                        className="no-arrow"
                                                        min="0"
                                                        step="0.01"
                                                        value={focusedFields[`debit-${index}`] ? (entry.debit != null ? entry.debit : '') : (entry.debit != null ? parseFloat(entry.debit).toFixed(2) : '')}
                                                        onChange={(e) => {
                                                            const inputValue = e.target.value;
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                const newValue = inputValue === '' ? null : (isNaN(parseFloat(inputValue)) ? prev[index].debit : parseFloat(inputValue));
                                                                updated[index].debit = newValue;
                                                                if (newValue) {
                                                                    updated[index].credit = null;
                                                                }
                                                                return updated;
                                                            });
                                                            updateTotals();
                                                        }}
                                                        onFocus={() => {
                                                            setFocusedFields(prev => ({ ...prev, [`debit-${index}`]: true }));
                                                            if (!entry.debit && !entry.credit) {
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                        onBlur={(e) => {
                                                            setFocusedFields(prev => {
                                                                const newFields = { ...prev };
                                                                delete newFields[`debit-${index}`];
                                                                return newFields;
                                                            });
                                                            if (e.target.value) {
                                                                setEntries(prev => {
                                                                    const updated = [...prev];
                                                                    updated[index].debit = parseFloat(parseFloat(e.target.value).toFixed(2));
                                                                    return updated;
                                                                });
                                                                updateTotals();
                                                            }
                                                        }}
                                                        onKeyDown={(e) => {
                                                            if (e.key === 'Tab' && !e.shiftKey) {
                                                                const inputValue = e.target.value;
                                                                const isEmpty = !inputValue || inputValue.trim() === '' || parseFloat(inputValue) === 0;

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
                                                                                const inputs = row.querySelectorAll('input[type="number"]');
                                                                                const creditInput = inputs[inputs.length - 1];
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
                                                        type="number"
                                                        className="no-arrow"
                                                        min="0"
                                                        step="0.01"
                                                        value={focusedFields[`credit-${index}`] ? (entry.credit != null ? entry.credit : '') : (entry.credit != null ? parseFloat(entry.credit).toFixed(2) : '')}
                                                        onChange={(e) => {
                                                            const inputValue = e.target.value;
                                                            setEntries(prev => {
                                                                const updated = [...prev];
                                                                const newValue = inputValue === '' ? null : (isNaN(parseFloat(inputValue)) ? prev[index].credit : parseFloat(inputValue));
                                                                updated[index].credit = newValue;
                                                                if (newValue) {
                                                                    updated[index].debit = null;
                                                                }
                                                                return updated;
                                                            });
                                                            updateTotals();
                                                        }}
                                                        onFocus={() => {
                                                            setFocusedFields(prev => ({ ...prev, [`credit-${index}`]: true }));
                                                            if (!entry.debit && !entry.credit) {
                                                                calculateRowAmounts(index);
                                                            }
                                                        }}
                                                        onBlur={(e) => {
                                                            setFocusedFields(prev => {
                                                                const newFields = { ...prev };
                                                                delete newFields[`credit-${index}`];
                                                                return newFields;
                                                            });
                                                            if (e.target.value) {
                                                                setEntries(prev => {
                                                                    const updated = [...prev];
                                                                    updated[index].credit = parseFloat(parseFloat(e.target.value).toFixed(2));
                                                                    return updated;
                                                                });
                                                                updateTotals();
                                                            }
                                                        }}
                                                        onKeyDown={(e) => {
                                                            if (e.key === 'Tab' && !e.shiftKey) {
                                                                const inputValue = e.target.value;
                                                                const isEmpty = !inputValue || inputValue.trim() === '' || parseFloat(inputValue) === 0;

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
                                        <td className="fw-bold">{totalDebit.toFixed(2)}</td>
                                        <td className="fw-bold">{totalCredit.toFixed(2)}</td>
                                        <td></td>
                                    </tr>
                                    <tr>
                                        <td colSpan="9" className="text-end fw-bold">BALANCE</td>
                                        <td colSpan="2" className="fw-bold text-center">{totalBalance.toFixed(2)}</td>
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
                                <Form.Label>Booker</Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text"><i className="fas fa-user"></i></span>
                                    <SearchableSelect
                                        value={bookerId}
                                        onChange={(value) => setBookerId(value ? parseInt(value) : null)}
                                        options={hasBookers ? bookers.map(b => ({ id: b.id, name: b.name })) : []}
                                        placeholder={hasBookers ? "Select Booker" : "No bookers available"}
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
                    <Button variant="secondary" type="button" onClick={() => navigate(-1)}>
                        <i className="fas fa-arrow-left me-1"></i> Back
                    </Button>
                    <div>
                        <Button
                            variant="primary"
                            type="button"
                            onClick={(e) => handleSubmit(e, 'submit')}
                            disabled={loading || isSubmitting || !hasBookers || !bookerId}
                            className="me-2"
                        >
                            {loading ? (
                                <>
                                    <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                    {isEditMode ? 'Updating...' : 'Submitting...'}
                                </>
                            ) : (
                                <>
                                    <i className="fas fa-save me-1"></i>
                                    {isEditMode ? 'Update Journal Voucher' : 'Submit Journal Entry'}
                                </>
                            )}
                        </Button>
                        <Button
                            variant="success"
                            type="button"
                            onClick={(e) => handleSubmit(e, 'submit_print')}
                            disabled={loading || isSubmitting || !hasBookers || !bookerId}
                        >
                            <i className="fas fa-print me-1"></i>
                            {isEditMode ? 'Update and Print' : 'Submit and Print'}
                        </Button>
                    </div>
                </div>
            </form>
        </div>
    );
};

export default HomeEntry;