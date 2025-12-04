import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import { usePageTitle } from '../../hooks/usePageTitle';

const ProcessBaseVoucher = () => {
    usePageTitle('Process Base Voucher');
    const { id } = useParams();
    const navigate = useNavigate();
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

    useEffect(() => {
        if (id) {
            loadBaseVoucher();
        }
    }, [id]);

    useEffect(() => {
        // Set default creditable date
        if (!creditableDate) {
            const today = new Date();
            setCreditableDate(today.toISOString().split('T')[0]);
        }
    }, []);

    // Initialize entries when voucher data loads
    useEffect(() => {
        if (voucherData && voucherData.entries && entries.length === 0) {
            const initializedEntries = voucherData.entries.map(entry => ({
                ...entry,
                processQuantity: entry.accountGroupId === 1 && !entry.isProcessed 
                    ? entry.availableQuantity 
                    : (entry.processQuantity || 0),
                calculatedDebit: 0,
                calculatedCredit: 0
            }));
            
            // Calculate initial amounts
            let updatedEntries = initializedEntries.map(entry => {
                if (entry.accountGroupId !== 1) {
                    const isChecked = entry.processQuantity > 0;
                    const quantity = entry.processQuantity || 0;
                    const availableQuantity = entry.availableQuantity || 0;
                    
                    let debitAmount = 0;
                    let creditAmount = 0;

                    if (isChecked && quantity > 0 && availableQuantity > 0) {
                        const ratio = quantity / availableQuantity;
                        const originalDebit = entry.debit || 0;
                        const originalCredit = entry.credit || 0;

                        if (originalDebit > 0) {
                            debitAmount = Math.round(originalDebit * ratio);
                        } else {
                            creditAmount = Math.round(originalCredit * ratio);
                        }
                    }

                    return {
                        ...entry,
                        calculatedDebit: debitAmount,
                        calculatedCredit: creditAmount
                    };
                }
                return entry;
            });

            // Update account entries
            let totalDebits = 0;
            let totalCredits = 0;
            let hasSelectedProducts = false;

            updatedEntries.forEach(entry => {
                if (entry.accountGroupId !== 1 && entry.processQuantity > 0) {
                    hasSelectedProducts = true;
                    totalDebits += entry.calculatedDebit || 0;
                    totalCredits += entry.calculatedCredit || 0;
                }
            });

            const difference = totalDebits - totalCredits;

            updatedEntries = updatedEntries.map(entry => {
                if (entry.accountGroupId === 1) {
                    const isProcessed = entry.isProcessed;
                    const shouldBeSelected = hasSelectedProducts && !isProcessed;
                    const newProcessQuantity = shouldBeSelected ? entry.availableQuantity : 0;

                    let accountDebit = 0;
                    let accountCredit = 0;

                    if (hasSelectedProducts) {
                        if (difference > 0) {
                            accountCredit = difference;
                            setAccountEntryAmount(difference);
                            setAccountEntryIsDebit(false);
                        } else if (difference < 0) {
                            accountDebit = Math.abs(difference);
                            setAccountEntryAmount(Math.abs(difference));
                            setAccountEntryIsDebit(true);
                        } else {
                            setAccountEntryAmount(0);
                        }
                    } else {
                        setAccountEntryAmount(0);
                    }

                    setAccountEntryIncluded(hasSelectedProducts);

                    return {
                        ...entry,
                        processQuantity: newProcessQuantity,
                        calculatedDebit: accountDebit,
                        calculatedCredit: accountCredit
                    };
                }
                return entry;
            });

            setEntries(updatedEntries);
        }
    }, [voucherData]);

    const loadBaseVoucher = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get(`/HomeApi/process-base-voucher/${id}`);
            if (response.data.success) {
                const data = response.data.data;
                setVoucherData(data);
                // Don't set entries here - let the useEffect handle it
            } else {
                setError(response.data.message || 'Failed to load base voucher');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load base voucher');
        } finally {
            setLoading(false);
        }
    };

    // Calculate entry amounts based on process quantity
    const calculateEntryAmounts = useCallback((entry) => {
        const isAccount = entry.accountGroupId === 1;
        const isChecked = entry.processQuantity > 0;
        const quantity = entry.processQuantity || 0;
        const availableQuantity = entry.availableQuantity || 0;
        
        let debitAmount = 0;
        let creditAmount = 0;

        if (isChecked && quantity > 0 && availableQuantity > 0 && !isAccount) {
            // For regular entries, calculate proportional values based on quantity
            const ratio = quantity / availableQuantity;
            const originalDebit = entry.debit || 0;
            const originalCredit = entry.credit || 0;

            if (originalDebit > 0) {
                debitAmount = Math.round(originalDebit * ratio);
            } else {
                creditAmount = Math.round(originalCredit * ratio);
            }
        }

        return {
            debit: debitAmount,
            credit: creditAmount
        };
    }, []);

    // Update account entries based on other entries
    const updateAccountEntries = useCallback((updatedEntries) => {
        // Calculate totals from non-account entries
        let totalDebits = 0;
        let totalCredits = 0;
        let hasSelectedProducts = false;

        updatedEntries.forEach(entry => {
            const isAccount = entry.accountGroupId === 1;
            if (!isAccount && entry.processQuantity > 0) {
                hasSelectedProducts = true;
                totalDebits += entry.calculatedDebit || 0;
                totalCredits += entry.calculatedCredit || 0;
            }
        });

        // Calculate the imbalance
        const difference = totalDebits - totalCredits;

        // Update account entries
        const finalEntries = updatedEntries.map(entry => {
            if (entry.accountGroupId === 1) {
                const isProcessed = entry.isProcessed;
                
                // Auto-check account entries if products are selected
                const shouldBeSelected = hasSelectedProducts && !isProcessed;
                const newProcessQuantity = shouldBeSelected ? entry.availableQuantity : 0;

                let accountDebit = 0;
                let accountCredit = 0;

                if (hasSelectedProducts) {
                    if (difference > 0) {
                        // More debits than credits, so account entry needs to be credit
                        accountCredit = difference;
                    } else if (difference < 0) {
                        // More credits than debits, so account entry needs to be debit
                        accountDebit = Math.abs(difference);
                    }
                }

                return {
                    ...entry,
                    processQuantity: newProcessQuantity,
                    calculatedDebit: accountDebit,
                    calculatedCredit: accountCredit
                };
            }
            return entry;
        });

        // Update account entry state
        if (hasSelectedProducts) {
            if (difference > 0) {
                setAccountEntryAmount(difference);
                setAccountEntryIsDebit(false);
            } else if (difference < 0) {
                setAccountEntryAmount(Math.abs(difference));
                setAccountEntryIsDebit(true);
            } else {
                setAccountEntryAmount(0);
            }
        } else {
            setAccountEntryAmount(0);
        }

        setAccountEntryIncluded(hasSelectedProducts);
        return finalEntries;
    }, []);


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

    // Calculate selected count and totals
    const selectedCount = useMemo(() => {
        return entries.filter(e => e.processQuantity > 0).length;
    }, [entries]);

    const totals = useMemo(() => {
        let totalDebit = 0;
        let totalCredit = 0;

        entries.forEach(entry => {
            if (entry.processQuantity > 0) {
                totalDebit += entry.calculatedDebit || 0;
                totalCredit += entry.calculatedCredit || 0;
            }
        });

        return { totalDebit, totalCredit };
    }, [entries]);

    const handleSubmit = async (e) => {
        e.preventDefault();
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
                return;
            }

            // Validate that all selected non-account entries have quantities > 0
            const invalidEntries = entries.filter(e => 
                e.processQuantity > 0 && 
                e.accountGroupId !== 1 && 
                e.processQuantity <= 0
            );

            if (invalidEntries.length > 0) {
                setError('All selected product entries must have a process quantity greater than zero.');
                setGenerating(false);
                return;
            }

            const model = {
                baseVoucherId: voucherData.baseVoucherId,
                projectId: voucherData.projectId,
                entries: entries.map(entry => ({
                    entryId: entry.entryId,
                    description: entry.description,
                    accountName: entry.accountName,
                    accountId: entry.accountId,
                    categoryId: entry.categoryId,
                    availableQuantity: entry.availableQuantity,
                    processQuantity: entry.processQuantity,
                    rate: entry.rate,
                    debit: entry.debit,
                    credit: entry.credit,
                    percent: entry.percent,
                    disc: entry.disc,
                    rbp: entry.rbp,
                    accountGroupId: entry.accountGroupId,
                    accountEntryAmount: entry.accountGroupId === 1 ? accountEntryAmount : 0,
                    accountEntryIsDebit: entry.accountGroupId === 1 ? accountEntryIsDebit : false
                })),
                accountEntryAmount: accountEntryAmount,
                accountEntryIsDebit: accountEntryIsDebit,
                accountEntryIncluded: accountEntryIncluded,
                creditableDate: creditableDate || new Date().toISOString().split('T')[0]
            };

            const response = await apiClient.post('/HomeApi/create-derived-voucher', model);
            
            if (response.data.success) {
                const voucherId = response.data.voucherId;
                setSuccess(response.data.message || `Derived voucher JV-${voucherId} created successfully`);
                setTimeout(() => {
                    if (voucherId) {
                        navigate(`/home/voucher-entry?jvId=${voucherId}`);
                    } else {
                        navigate('/home/base-vouchers');
                    }
                }, 2000);
            } else {
                setError(response.data.message || 'Failed to create derived voucher');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to create derived voucher');
        } finally {
            setGenerating(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value || 0);
    };

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
                                        <th width="8%">Entry ID</th>
                                        <th width="20%">Account</th>
                                        <th width="22%">Description</th>
                                        <th width="8%">Available</th>
                                        <th width="10%">Process Qty</th>
                                        <th width="7%">Rate</th>
                                        <th width="10%">Debit</th>
                                        <th width="10%">Credit</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries.map((entry, index) => {
                                        const isProcessed = entry.isProcessed;
                                        const isAccountEntry = entry.accountGroupId === 1;
                                        const rowClass = isProcessed 
                                            ? 'bg-light' 
                                            : (isAccountEntry ? 'table-info' : '');
                                        const isSelected = entry.processQuantity > 0;
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
                                                        title={isAccountEntry ? 'This account entry is automatically selected when any product is selected' : ''}
                                                    />
                                                </td>
                                                <td>{entry.entryId}</td>
                                                <td>
                                                    {entry.accountName || 'N/A'}
                                                    {isAccountEntry && (
                                                        <span className="badge bg-info ms-1">Account</span>
                                                    )}
                                                </td>
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
                                        <td colSpan="7" className="text-end">Totals:</td>
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

                    <div className="row mb-3">
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
                </div>

                <div className="d-flex justify-content-between">
                    <div>
                        <span className="fw-bold me-2">Selected Items:</span>
                        <span id="selectedCount" className="badge bg-primary">{selectedCount}</span>
                    </div>
                    <Button 
                        type="submit" 
                        variant="success" 
                        id="submitBtn"
                        disabled={generating || selectedCount === 0}
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
            </Form>
        </div>
    );
};

export default ProcessBaseVoucher;
