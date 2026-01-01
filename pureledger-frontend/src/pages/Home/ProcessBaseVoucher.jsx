import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import { usePageTitle } from '../../hooks/usePageTitle';

const ProcessBaseVoucher = () => {
    usePageTitle('Process Base Voucher');
    const { id } = useParams();
    const navigate = useNavigate();
    
    // Ref to prevent duplicate submissions
    const isSubmittingRef = useRef(false);
    
    const [voucherData, setVoucherData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [entries, setEntries] = useState([]);
    const [accountEntryAmount, setAccountEntryAmount] = useState(0);
    const [accountEntryIsDebit, setAccountEntryIsDebit] = useState(false);
    const [accountEntryIncluded, setAccountEntryIncluded] = useState(true);
    const [creditableDate, setCreditableDate] = useState('');
    const [bookers, setBookers] = useState([]);
    const [bookerId, setBookerId] = useState(null);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [hasSubmitted, setHasSubmitted] = useState(false);

    // Calculate entry amounts using the same formula as Entry.jsx
    const calculateEntryAmounts = useCallback((entry) => {
        const isAccount = entry.accountGroupId === 1;
        const quantity = entry.processQuantity || 0;
        const availableQuantity = entry.availableQuantity || 0;
        
        let debitAmount = 0;
        let creditAmount = 0;

        if (!isAccount && quantity > 0 && availableQuantity > 0) {
            // Get values from entry (same as Entry.jsx calculation)
            const rbp = entry.rbp === 'Y' || entry.rbp === 'y';
            const qty = parseFloat(quantity) || 0;
            const rate = parseFloat(entry.rate) || 0;
            const percent = parseFloat(entry.percent) || 0;
            const pkg = parseFloat(entry.pkg) || 0; // Use pkg directly from API
            
            // Get original debit/credit to determine direction (not for calculation)
            const originalDebit = parseFloat(entry.debit) || 0;
            const originalCredit = parseFloat(entry.credit) || 0;
            
            // Calculate total amount using the formula: pkg * qty * rate (if RBP is Y)
            let totalAmount = 0;
            if (rbp && pkg > 0) {
                totalAmount = pkg * qty * rate;
            } else {
                totalAmount = qty * rate;
            }
            
            // Calculate discount based on total amount and percent
            const discount = Math.round(((totalAmount * percent) / 100) * 100) / 100;
            
            // Calculate net amount (total amount - discount)
            const netAmount = totalAmount - discount;
            
            // Round net amount to 2 decimal places
            const roundedNetAmount = Math.round(netAmount * 100) / 100;
            
            // Determine if it should be debit or credit based on original entry
            // Use the direction from the original entry, not the existing amount
            if (originalDebit > 0) {
                debitAmount = roundedNetAmount;
            } else if (originalCredit > 0) {
                creditAmount = roundedNetAmount;
            }
        }

        return {
            debit: parseFloat(debitAmount) || 0,
            credit: parseFloat(creditAmount) || 0
        };
    }, []);

    // Update account entries based on other entries
    const updateAccountEntries = useCallback((updatedEntries) => {
        // Calculate totals from non-account entries with proper rounding
        let totalDebits = 0;
        let totalCredits = 0;
        let hasSelectedProducts = false;

        updatedEntries.forEach(entry => {
            const isAccount = entry.accountGroupId === 1;
            if (!isAccount && entry.processQuantity > 0) {
                hasSelectedProducts = true;
                // Use calculated values, ensuring they're numbers
                const debit = parseFloat(entry.calculatedDebit) || 0;
                const credit = parseFloat(entry.calculatedCredit) || 0;
                totalDebits += debit;
                totalCredits += credit;
            }
        });

        // Round totals to 2 decimal places to avoid floating point issues
        totalDebits = Math.round(totalDebits * 100) / 100;
        totalCredits = Math.round(totalCredits * 100) / 100;

        // Calculate the imbalance
        const difference = totalDebits - totalCredits;
        // Round difference to 2 decimal places
        const roundedDifference = Math.round(difference * 100) / 100;

        // Update account entries
        const finalEntries = updatedEntries.map(entry => {
            if (entry.accountGroupId === 1) {
                const isProcessed = entry.isProcessed;
                
                let accountDebit = 0;
                let accountCredit = 0;

                if (hasSelectedProducts && Math.abs(roundedDifference) > 0.001) {
                    if (roundedDifference > 0) {
                        // More debits than credits, so account entry needs to be credit
                        accountCredit = roundedDifference;
                    } else {
                        // More credits than debits, so account entry needs to be debit
                        accountDebit = Math.abs(roundedDifference);
                    }
                }

                // Round to 2 decimal places
                accountDebit = Math.round(accountDebit * 100) / 100;
                accountCredit = Math.round(accountCredit * 100) / 100;
                
                // Auto-check account entries if products are selected AND amount > 0
                const hasAmount = accountDebit > 0 || accountCredit > 0;
                const shouldBeSelected = hasSelectedProducts && hasAmount && !isProcessed;
                const newProcessQuantity = shouldBeSelected ? entry.availableQuantity : 0;

                return {
                    ...entry,
                    processQuantity: newProcessQuantity,
                    calculatedDebit: parseFloat(accountDebit) || 0,
                    calculatedCredit: parseFloat(accountCredit) || 0
                };
            }
            return entry;
        });

        // Update account entry state with rounded values
        if (hasSelectedProducts && Math.abs(roundedDifference) > 0.001) {
            if (roundedDifference > 0) {
                setAccountEntryAmount(parseFloat(roundedDifference) || 0);
                setAccountEntryIsDebit(false);
            } else {
                setAccountEntryAmount(parseFloat(Math.abs(roundedDifference)) || 0);
                setAccountEntryIsDebit(true);
            }
        } else {
            setAccountEntryAmount(0);
            setAccountEntryIsDebit(false);
        }

        setAccountEntryIncluded(hasSelectedProducts);
        return finalEntries;
    }, []);

    // Load base voucher data
    const loadBaseVoucher = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get(`/HomeApi/process-base-voucher/${id}`);
            if (response.data.success) {
                const data = response.data.data;
                setVoucherData(data);
            } else {
                setError(response.data.message || 'Failed to load base voucher');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load base voucher');
        } finally {
            setLoading(false);
        }
    };

    // Load bookers
    const loadBookers = async () => {
        try {
            const response = await apiClient.get('/MetaDataApi/bookers');
            if (response.data.success) {
                // Transform to match the format used in Entry.jsx (id, name)
                const enabledBookers = (response.data.data || [])
                    .filter(b => b.isEnabled)
                    .map(b => ({
                        id: b.bookerID || b.id,
                        name: b.bookerName || b.name
                    }));
                setBookers(enabledBookers);
                // Set first booker as default
                if (enabledBookers.length > 0 && !bookerId) {
                    setBookerId(enabledBookers[0].id);
                }
            }
        } catch (err) {
            console.error('Error loading bookers:', err);
        }
    };

    // Initialize entries when voucher data loads
    useEffect(() => {
        if (voucherData && voucherData.entries && entries.length === 0) {
            const initializedEntries = voucherData.entries.map(entry => ({
                ...entry,
                // Ensure all numeric values are properly parsed
                rate: parseFloat(entry.rate) || 0,
                debit: parseFloat(entry.debit) || 0,
                credit: parseFloat(entry.credit) || 0,
                percent: parseFloat(entry.percent) || 0,
                disc: parseFloat(entry.disc) || 0,
                pkg: parseFloat(entry.pkg) || 0,
                availableQuantity: parseInt(entry.availableQuantity) || 0,
                processQuantity: entry.accountGroupId === 1 && !entry.isProcessed 
                    ? parseInt(entry.availableQuantity) || 0
                    : (parseInt(entry.processQuantity) || 0),
                accountGroupId: parseInt(entry.accountGroupId) || 0,
                calculatedDebit: 0,
                calculatedCredit: 0
            }));
            
            // Calculate initial amounts for non-account entries
            let updatedEntries = initializedEntries.map(entry => {
                if (entry.accountGroupId !== 1) {
                    const amounts = calculateEntryAmounts(entry);
                    return {
                        ...entry,
                        calculatedDebit: amounts.debit,
                        calculatedCredit: amounts.credit
                    };
                }
                return entry;
            });

            // Update account entries
            updatedEntries = updateAccountEntries(updatedEntries);
            setEntries(updatedEntries);
        }
    }, [voucherData, calculateEntryAmounts, updateAccountEntries]);

    // Set creditable date from base voucher when voucher data loads
    useEffect(() => {
        if (voucherData && voucherData.creditableDate) {
            setCreditableDate(voucherData.creditableDate);
        }
    }, [voucherData]);

    // Load data on mount
    useEffect(() => {
        if (id) {
            loadBaseVoucher();
        }
        loadBookers();
    }, [id]);

    // Set default creditable date (fallback if voucher data not available)
    useEffect(() => {
        if (!creditableDate && !voucherData) {
            const today = new Date();
            setCreditableDate(today.toISOString().split('T')[0]);
        }
    }, [creditableDate]);

    // Handle quantity change
    const handleQuantityChange = (index, value) => {
        const updatedEntries = [...entries];
        const entry = updatedEntries[index];
        const isAccount = entry.accountGroupId === 1;

        // Prevent manual changes to account entries
        if (isAccount) {
            return;
        }

        const qty = parseInt(value) || 0;
        const maxQty = entry.availableQuantity || 0;
        
        if (qty < 0) {
            entry.processQuantity = 0;
        } else if (qty > maxQty) {
            entry.processQuantity = maxQty;
        } else {
            entry.processQuantity = qty;
        }

        // Recalculate all amounts
        let recalculatedEntries = updatedEntries.map(e => {
            if (e.accountGroupId !== 1) {
                const amounts = calculateEntryAmounts(e);
                return {
                    ...e,
                    calculatedDebit: amounts.debit,
                    calculatedCredit: amounts.credit
                };
            }
            return e;
        });

        // Update account entries
        recalculatedEntries = updateAccountEntries(recalculatedEntries);
        setEntries(recalculatedEntries);
    };

    // Handle checkbox change
    const handleCheckboxChange = (index, checked) => {
        const updatedEntries = [...entries];
        const entry = updatedEntries[index];
        const isAccount = entry.accountGroupId === 1;

        // Prevent manual changes to account entries
        if (isAccount) {
            return;
        }

        if (checked) {
            // Set to max quantity when checked
            entry.processQuantity = entry.availableQuantity;
        } else {
            // Set to 0 when unchecked
            entry.processQuantity = 0;
        }

        // Recalculate all amounts
        let recalculatedEntries = updatedEntries.map(e => {
            if (e.accountGroupId !== 1) {
                const amounts = calculateEntryAmounts(e);
                return {
                    ...e,
                    calculatedDebit: amounts.debit,
                    calculatedCredit: amounts.credit
                };
            }
            return e;
        });

        // Update account entries
        recalculatedEntries = updateAccountEntries(recalculatedEntries);
        setEntries(recalculatedEntries);
    };

    // Handle select all
    const handleSelectAll = () => {
        let updatedEntries = entries.map(entry => {
            const isAccount = entry.accountGroupId === 1;
            const isProcessed = entry.isProcessed;
            
            // Only select non-account, non-processed entries
            if (!isAccount && !isProcessed) {
                return {
                    ...entry,
                    processQuantity: entry.availableQuantity
                };
            }
            return entry;
        });

        // Recalculate amounts
        updatedEntries = updatedEntries.map(e => {
            if (e.accountGroupId !== 1) {
                const amounts = calculateEntryAmounts(e);
                return {
                    ...e,
                    calculatedDebit: amounts.debit,
                    calculatedCredit: amounts.credit
                };
            }
            return e;
        });

        // Update account entries
        updatedEntries = updateAccountEntries(updatedEntries);
        setEntries(updatedEntries);
    };

    // Handle deselect all
    const handleDeselectAll = () => {
        let updatedEntries = entries.map(entry => {
            const isAccount = entry.accountGroupId === 1;
            // Keep account entries, but reset others
            if (!isAccount) {
                return {
                    ...entry,
                    processQuantity: 0
                };
            }
            return entry;
        });

        // Recalculate amounts
        updatedEntries = updatedEntries.map(e => {
            if (e.accountGroupId !== 1) {
                const amounts = calculateEntryAmounts(e);
                return {
                    ...e,
                    calculatedDebit: amounts.debit,
                    calculatedCredit: amounts.credit
                };
            }
            return e;
        });

        // Update account entries
        updatedEntries = updateAccountEntries(updatedEntries);
        setEntries(updatedEntries);
    };

    // Handle max quantities
    const handleMaxQuantities = () => {
        let updatedEntries = entries.map(entry => {
            const isAccount = entry.accountGroupId === 1;
            const isProcessed = entry.isProcessed;
            
            // Only set max for checked non-account, non-processed entries
            if (!isAccount && !isProcessed && entry.processQuantity > 0) {
                return {
                    ...entry,
                    processQuantity: entry.availableQuantity
                };
            }
            return entry;
        });

        // Recalculate amounts
        updatedEntries = updatedEntries.map(e => {
            if (e.accountGroupId !== 1) {
                const amounts = calculateEntryAmounts(e);
                return {
                    ...e,
                    calculatedDebit: amounts.debit,
                    calculatedCredit: amounts.credit
                };
            }
            return e;
        });

        // Update account entries
        updatedEntries = updateAccountEntries(updatedEntries);
        setEntries(updatedEntries);
    };

    // Calculate selected count
    const selectedCount = useMemo(() => {
        return entries.filter(e => e.processQuantity > 0).length;
    }, [entries]);

     // Calculate totals
    const totals = useMemo(() => {
        let totalDebit = 0;
        let totalCredit = 0;

        entries.forEach(entry => {
            // Include entries that are selected (processQuantity > 0) or account entries with calculated amounts
            const isSelected = entry.processQuantity > 0;
            const isAccount = entry.accountGroupId === 1;
            
            if (isSelected || (isAccount && (entry.calculatedDebit > 0 || entry.calculatedCredit > 0))) {
                const debit = parseFloat(entry.calculatedDebit) || 0;
                const credit = parseFloat(entry.calculatedCredit) || 0;
                totalDebit += debit;
                totalCredit += credit;
            }
        });

        // Round to 2 decimal places
        totalDebit = Math.round(totalDebit * 100) / 100;
        totalCredit = Math.round(totalCredit * 100) / 100;

        return { totalDebit, totalCredit };
    }, [entries]);

    // Handle form submission
    const handleSubmit = async (e) => {
        e.preventDefault();
        
        // Prevent duplicate submissions
        if (isSubmittingRef.current || isSubmitting) return;
        isSubmittingRef.current = true;
        setIsSubmitting(true);
        setHasSubmitted(false);
        
        setError('');
        setSuccess('');
        setGenerating(true);

        try {
            // Count selected non-account entries
            const selectedProductEntries = entries.filter(e => 
                e.processQuantity > 0 && e.accountGroupId !== 1
            );

            if (selectedProductEntries.length === 0) {
                setError('Please select at least one product entry to process.');
                setGenerating(false);
                isSubmittingRef.current = false;
                return;
            }

            // Validate booker is selected
            if (!bookerId || bookerId <= 0) {
                setError('Please select a booker.');
                setGenerating(false);
                isSubmittingRef.current = false;
                return;
            }

            // Validate that all selected non-account entries have valid quantities and amounts
            const invalidEntries = entries.filter(e => 
                e.processQuantity > 0 && 
                e.accountGroupId !== 1 && 
                (e.processQuantity <= 0 || 
                 (e.calculatedDebit <= 0 && e.calculatedCredit <= 0))
            );

            if (invalidEntries.length > 0) {
                setError('All selected product entries must have a valid process quantity and calculated amounts.');
                setGenerating(false);
                isSubmittingRef.current = false;
                return;
            }
            
            // Validate that at least one entry has calculated amounts
            const entriesWithAmounts = entries.filter(e => 
                e.processQuantity > 0 && 
                (e.calculatedDebit > 0 || e.calculatedCredit > 0)
            );
            
            if (entriesWithAmounts.length === 0) {
                setError('No entries have valid calculated amounts. Please check your selections.');
                setGenerating(false);
                isSubmittingRef.current = false;
                return;
            }

            // Find account entry to get its calculated amounts
            const accountEntry = entries.find(e => e.accountGroupId === 1 && e.processQuantity > 0);
            const finalAccountEntryAmount = parseFloat(accountEntry 
                ? (Math.round((accountEntry.calculatedDebit > 0 ? accountEntry.calculatedDebit : accountEntry.calculatedCredit) * 100) / 100)
                : (Math.round(accountEntryAmount * 100) / 100)) || 0;
            const finalAccountEntryIsDebit = accountEntry
                ? (accountEntry.calculatedDebit > 0)
                : accountEntryIsDebit;
            
            // Final validation: ensure debits and credits balance
            const totalDebit = totals.totalDebit;
            const totalCredit = totals.totalCredit;
            const difference = Math.abs(totalDebit - totalCredit);
            
            if (difference > 0.01) {
                setError(`Debits and credits do not balance. Difference: ${difference.toFixed(2)}. Please check your entries.`);
                setGenerating(false);
                isSubmittingRef.current = false;
                return;
            }

            const model = {
                baseVoucherId: voucherData.baseVoucherId,
                projectId: voucherData.projectId,
                entries: entries.map(entry => ({
                    entryId: entry.entryId,
                    description: entry.description || '',
                    accountName: entry.accountName || '',
                    accountId: parseInt(entry.accountId) || 0,
                    categoryId: parseInt(entry.categoryId) || 0,
                    availableQuantity: parseInt(entry.availableQuantity) || 0,
                    processQuantity: parseInt(entry.processQuantity) || 0,
                    rate: parseFloat(entry.rate) || 0,
                    debit: parseFloat(entry.debit) || 0,
                    credit: parseFloat(entry.credit) || 0,
                    percent: parseFloat(entry.percent) || 0,
                    disc: parseFloat(entry.disc) || 0,
                    rbp: entry.rbp || 'Y',
                    pkg: parseFloat(entry.pkg) || 0,
                    accountGroupId: parseInt(entry.accountGroupId) || 0,
                    // Send calculated values from frontend to ensure backend uses correct amounts
                    calculatedDebit: parseFloat(entry.calculatedDebit) || 0,
                    calculatedCredit: parseFloat(entry.calculatedCredit) || 0,
                    // Use calculated values from the entry itself
                    accountEntryAmount: entry.accountGroupId === 1 
                        ? parseFloat(entry.calculatedDebit > 0 ? entry.calculatedDebit : entry.calculatedCredit) || 0
                        : 0,
                    accountEntryIsDebit: entry.accountGroupId === 1 
                        ? (entry.calculatedDebit > 0)
                        : false
                })),
                accountEntryAmount: parseFloat(finalAccountEntryAmount) || 0,
                accountEntryIsDebit: finalAccountEntryIsDebit,
                accountEntryIncluded: accountEntryIncluded,
                creditableDate: creditableDate || new Date().toISOString().split('T')[0],
                bookerId: bookerId ? parseInt(bookerId) : null
            };

            console.log('Submitting derived voucher with model:', {
                baseVoucherId: model.baseVoucherId,
                entriesCount: model.entries.length,
                selectedEntries: model.entries.filter(e => e.processQuantity > 0).length,
                accountEntryAmount: model.accountEntryAmount,
                bookerId: model.bookerId
            });
            
            const response = await apiClient.post('/HomeApi/create-derived-voucher', model);
            
            console.log('Response received:', response.data);
            
            if (response.data.success) {
                const voucherId = response.data.voucherId;
                setHasSubmitted(true);
                setSuccess(response.data.message || `Derived voucher JV-${voucherId} created successfully`);
                setTimeout(() => {
                    if (voucherId) {
                        // Open print page in new tab (accessible to all users)
                        const printUrl = `/reporting/print-voucher?voucherId=${voucherId}&type=JV`;
                        window.open(printUrl, '_blank');
                    }
                    // Navigate to base vouchers with project selected
                    navigate(`/home/base-vouchers?projectId=${voucherData.projectId}`);
                }, 2000);
            } else {
                setError(response.data.message || 'Failed to create derived voucher');
            }
        } catch (err) {
            console.error('Error creating derived voucher:', err);
            const errorMessage = err.response?.data?.message || 
                                err.response?.data?.errors?.join(', ') ||
                                err.message || 
                                'Failed to create derived voucher. Please try again.';
            setError(errorMessage);
        } finally {
            setGenerating(false);
            // Reset submission flag after a delay to prevent immediate re-submission
            setTimeout(() => {
                isSubmittingRef.current = false;
                setIsSubmitting(false);
            }, 1000);
        }
    };

    // Format currency - show up to 2 decimal places
    const formatCurrency = (value) => {
        const numValue = parseFloat(value) || 0;
        // Show up to 2 decimal places (don't round off the actual stored value, just display)
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(numValue);
    };

    // Format number
    const formatNumber = (value) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 4
        }).format(value || 0);
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    if (!voucherData) {
        return (
            <div className="container-fluid">
                <Alert variant="danger">
                    <i className="fas fa-exclamation-circle me-2"></i>Base voucher not found
                </Alert>
                <Link to="/home/base-vouchers" className="btn btn-secondary">
                    <i className="fas fa-arrow-left me-1"></i> Back to Base Vouchers
                </Link>
            </div>
        );
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2>
                    <i className="fas fa-cogs me-2"></i>Process Base Voucher JV-{voucherData.baseVoucherId}
                </h2>
                <Link to="/home/base-vouchers" className="btn btn-secondary">
                    <i className="fas fa-arrow-left me-1"></i> Back to Base Vouchers
                </Link>
            </div>

            <Alert variant="info">
                <i className="fas fa-info-circle me-2"></i>
                Select items to include in the new derived voucher. You can process all or part of each entry.
            </Alert>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            {success && (
                <Alert variant="success" dismissible onClose={() => setSuccess('')}>
                    <i className="fas fa-check-circle me-2"></i>{success}
                </Alert>
            )}

            <Form id="processingForm" onSubmit={handleSubmit}>
                <input type="hidden" name="BaseVoucherId" value={voucherData.baseVoucherId} />
                <input type="hidden" name="ProjectId" value={voucherData.projectId} />
                
                <Card className="shadow mb-4">
                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">Available Entries</h6>
                        <div>
                            <Button 
                                type="button"
                                variant="outline-primary" 
                                size="sm" 
                                className="me-1" 
                                onClick={handleSelectAll}
                            >
                                <i className="fas fa-check-square me-1"></i> Select All
                            </Button>
                            <Button 
                                type="button"
                                variant="outline-secondary" 
                                size="sm" 
                                className="me-1" 
                                onClick={handleDeselectAll}
                            >
                                <i className="fas fa-square me-1"></i> Deselect All
                            </Button>
                            <Button 
                                type="button"
                                variant="outline-info" 
                                size="sm" 
                                onClick={handleMaxQuantities}
                            >
                                <i className="fas fa-fill me-1"></i> Max Quantities
                            </Button>
                        </div>
                    </Card.Header>
                    <Card.Body>
                        <div className="table-responsive">
                            <Table bordered hover id="entriesTable">
                                <thead className="table-primary">
                                    <tr>
                                        <th width="5%">Select</th>
                                        <th width="7%">Entry ID</th>
                                        <th width="18%">Account</th>
                                        <th width="6%">Pkg</th>
                                        <th width="18%">Description</th>
                                        <th width="7%">Available</th>
                                        <th width="8%">Process Qty</th>
                                        <th width="6%">Rate</th>
                                        <th width="6%">Discount %</th>
                                        <th width="9%">Debit</th>
                                        <th width="9%">Credit</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries.map((entry, index) => {
                                        const isProcessed = entry.isProcessed;
                                        const isAccountEntry = entry.accountGroupId === 1;
                                        const rowClass = isProcessed 
                                            ? 'bg-light' 
                                            : (isAccountEntry ? 'table-info' : '');
                                        
                                        // For account entries, check if products are selected and amount > 0
                                        // For regular entries, check if processQuantity > 0
                                        const isSelected = isAccountEntry 
                                            ? (entry.processQuantity > 0 && (entry.calculatedDebit > 0 || entry.calculatedCredit > 0))
                                            : (entry.processQuantity > 0);
                                        
                                        const rowActiveClass = isSelected && !isProcessed ? 'table-active' : '';

                                        return (
                                            <tr 
                                                key={entry.entryId} 
                                                className={`entry-row ${rowClass} ${rowActiveClass}`}
                                            >
                                                <td className="text-center">
                                                    <Form.Check
                                                        type="checkbox"
                                                        className="entry-select"
                                                        checked={isSelected}
                                                        disabled={isProcessed || isAccountEntry}
                                                        onChange={(e) => handleCheckboxChange(index, e.target.checked)}
                                                        style={isAccountEntry ? { 
                                                            opacity: 0.7, 
                                                            cursor: 'not-allowed' 
                                                        } : {}}
                                                        title={isAccountEntry ? 'This account entry is automatically selected when products are selected and has a calculated amount' : ''}
                                                    />
                                                </td>
                                                <td>{entry.entryId}</td>
                                                <td>
                                                    {entry.accountName || 'N/A'}
                                                    {isAccountEntry && (
                                                        <span className="badge bg-info ms-1">Account</span>
                                                    )}
                                                </td>
                                                <td className="text-end">{formatNumber(entry.pkg || 0)}</td>
                                                <td>{entry.description || '-'}</td>
                                                <td className="text-end">{entry.availableQuantity}</td>
                                                <td>
                                                    <Form.Control
                                                        type="number"
                                                        className="process-qty"
                                                        size="sm"
                                                        min="0"
                                                        max={entry.availableQuantity}
                                                        value={entry.processQuantity || 0}
                                                        onChange={(e) => handleQuantityChange(index, e.target.value)}
                                                        disabled={isProcessed || isAccountEntry}
                                                    />
                                                </td>
                                                <td className="text-end">{formatNumber(entry.rate || 0)}</td>
                                                <td className="text-end">{formatNumber(entry.percent || 0)}</td>
                                                <td className="text-end entry-debit">
                                                    {formatCurrency(entry.calculatedDebit || 0)}
                                                </td>
                                                <td className="text-end entry-credit">
                                                    {formatCurrency(entry.calculatedCredit || 0)}
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot className="table-secondary fw-bold">
                                    <tr>
                                        <td colSpan="9" className="text-end">Totals:</td>
                                        <td className="text-end" id="totalDebit">
                                            {formatCurrency(totals.totalDebit)}
                                        </td>
                                        <td className="text-end" id="totalCredit">
                                            {formatCurrency(totals.totalCredit)}
                                        </td>
                                    </tr>
                                </tfoot>
                            </Table>
                        </div>
                    </Card.Body>
                </Card>

                <div className="mb-3">
                    <div className="d-flex align-items-end gap-3 mb-2">
                        <span className="fw-bold">Selected Items:</span>
                        <span id="selectedCount" className="badge bg-primary">{selectedCount}</span>
                    </div>
                    <div className="row">
                        <div className="col-md-4">
                            <Form.Label htmlFor="CreditableDate">Creditable Date</Form.Label>
                            <InputGroup>
                                <InputGroup.Text 
                                    className="calendar-icon"
                                    style={{ cursor: 'pointer' }}
                                    onClick={() => {
                                        const dateInput = document.getElementById('CreditableDate');
                                        if (dateInput && !dateInput.disabled) {
                                            dateInput.showPicker();
                                        }
                                    }}
                                >
                                    <i className="fas fa-calendar-alt"></i>
                                </InputGroup.Text>
                                <Form.Control
                                    id="CreditableDate"
                                    name="CreditableDate"
                                    type="date"
                                    value={creditableDate}
                                    onChange={(e) => setCreditableDate(e.target.value)}
                                    required
                                />
                            </InputGroup>
                        </div>
                        {bookers.length > 0 && (
                            <div className="col-md-4">
                                <Form.Label htmlFor="BookerId">Booker <span className="text-danger">*</span></Form.Label>
                                <Form.Select
                                    id="BookerId"
                                    name="BookerId"
                                    value={bookerId || ''}
                                    onChange={(e) => setBookerId(e.target.value ? parseInt(e.target.value) : null)}
                                    required
                                >
                                    {bookers.map(booker => (
                                        <option key={booker.id} value={booker.id}>
                                            {booker.name}
                                        </option>
                                    ))}
                                </Form.Select>
                            </div>
                        )}
                        <div className="col-md-4 d-flex align-items-end">
                            <Button 
                                type="submit" 
                                variant="success" 
                                id="submitBtn"
                                className="w-100"
                                disabled={generating || isSubmitting || selectedCount === 0 || hasSubmitted}
                            >
                                {generating ? (
                                    <>
                                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                        Processing...
                                    </>
                                ) : (
                                    <>
                                        <i className="fas fa-share-alt me-1"></i> Create Derived Voucher
                                    </>
                                )}
                            </Button>
                        </div>
                    </div>
                </div>
            </Form>
        </div>
    );
};

export default ProcessBaseVoucher;
