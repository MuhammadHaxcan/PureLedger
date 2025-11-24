import { useState, useEffect, useRef, useCallback } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Card, Form, Button, Table, Alert, Modal } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import './Entry.css';

const HomeEntry = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const editingVoucherId = searchParams.get('voucherId') ? parseInt(searchParams.get('voucherId')) : null;
  const isEditMode = editingVoucherId != null;

  // State
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
    accountGroup: null, // 'product' or 'account'
    accountType: null,
    dataPkg: null,
    totalAmount: 0
  }]);
  
  // Store categories and accounts for each row
  const [rowCategories, setRowCategories] = useState({});
  const [rowAccounts, setRowAccounts] = useState({});
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [validationErrors, setValidationErrors] = useState([]);
  const [hasBookers, setHasBookers] = useState(false);

  // Refs for select dropdowns
  const selectDropdownsRef = useRef({});

  // Load initial data
  useEffect(() => {
    loadEntryData();
    if (isEditMode) {
      loadVoucherForEdit();
    }
  }, []);

  // Load categories when project changes
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

  // Update due date when entry date or credit days change
  useEffect(() => {
    if (entryDate && creditDays !== null) {
      const date = new Date(entryDate);
      date.setDate(date.getDate() + creditDays);
      setDueDate(date.toISOString().split('T')[0]);
    }
  }, [entryDate, creditDays]);

  const loadEntryData = async () => {
    try {
      const response = await apiClient.get('/HomeApi/entry-data');
      if (response.data.success) {
        setProjects(response.data.projects || []);
        setBookers(response.data.bookers || []);
        setHasBookers((response.data.bookers || []).length > 0);
        if (response.data.selectedProjectId) {
          setSelectedProjectId(response.data.selectedProjectId);
        }
        // Set first booker by default if available
        if (response.data.bookers && response.data.bookers.length > 0) {
          setBookerId(response.data.bookers[0].id);
        }
      }
    } catch (error) {
      console.error('Error loading entry data:', error);
      setError('Failed to load entry data');
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
        setHasBookers((bookData || []).length > 0);
        setSelectedProjectId(projId);
        setEntryDate(model.entryDate || new Date().toISOString().split('T')[0]);
        setCreditDays(model.creditDays || 0);
        setDueDate(model.dueDate || new Date().toISOString().split('T')[0]);
        setBookerId(model.bookerId);
        setRequireQuantity(model.requireQuantity !== false);
        setIsBaseVoucher(model.isBaseVoucher || false);
        
        // Load entries with category and account data
        if (model.entries && model.entries.length > 0) {
          // Load categories for the project
          const categories = await loadCategories(projId, 0);
          const categoriesMap = {};
          const accountsMap = {};
          
          const loadedEntries = await Promise.all(model.entries.map(async (entry, index) => {
            // Load product details for each entry
            let accountGroup = null;
            let accountType = null;
            let dataPkg = null;
            
            // Store categories for this row
            categoriesMap[index] = categories;
            
            // Load accounts for this category
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
                console.error('Error loading product details:', err);
              }
            }
            
            return {
              categoryId: entry.categoryId,
              accountId: entry.accountId,
              pkg: entry.pkg || dataPkg,
              rbp: entry.rbp || 'Y',
              des: entry.des || '',
              qty: entry.qty || null,
              rate: entry.rate || null,
              percent: entry.percent || 0,
              disc: entry.disc || 0,
              debit: entry.debit || null,
              credit: entry.credit || null,
              accountGroup,
              accountType,
              dataPkg,
              totalAmount: 0
            };
          }));
          setEntries(loadedEntries);
          setRowCategories(categoriesMap);
          setRowAccounts(accountsMap);
        }
      }
    } catch (error) {
      console.error('Error loading voucher for edit:', error);
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
      console.error('Error loading categories:', error);
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
      console.error('Error loading accounts:', error);
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
        
        // Update the entry with product details
        setEntries(prev => {
          const updated = [...prev];
          if (updated[rowIndex]) {
            updated[rowIndex] = {
              ...updated[rowIndex],
              accountGroup,
              accountType: product.accountTypeName || null,
              dataPkg: product.pkg || null,
              pkg: updated[rowIndex].pkg || product.pkg || null
            };
            
            // Set rate if not in edit mode and rate is available
            if (!isEditMode && product.saleRate !== null && product.saleRate !== undefined) {
              updated[rowIndex].rate = parseFloat(product.saleRate).toFixed(4);
            }
          }
          return updated;
        });
        
        return product;
      }
    } catch (error) {
      console.error('Error loading product details:', error);
    }
    return null;
  };

  const handleProjectChange = async (projectId) => {
    setSelectedProjectId(projectId);
    // Clear all entries' categories and accounts when project changes
    setEntries(prev => prev.map(entry => ({
      ...entry,
      categoryId: null,
      accountId: null,
      accountGroup: null,
      accountType: null
    })));
    
    // Load categories for all rows
    if (projectId) {
      const categories = await loadCategories(projectId, 0);
      const categoriesMap = {};
      entries.forEach((_, index) => {
        categoriesMap[index] = categories;
      });
      setRowCategories(categoriesMap);
      // Clear accounts
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
        accountId: null, // Clear account when category changes
        accountGroup: null,
        accountType: null
      };
      return updated;
    });
    
    // Load accounts for this category
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
      const updated = [...prev];
      updated[rowIndex] = {
        ...updated[rowIndex],
        accountId
      };
      return updated;
    });
    
    // Load product details
    if (accountId) {
      await loadProductDetails(accountId, rowIndex);
      // Trigger calculations for product accounts
      setTimeout(() => {
        calculateRowAmounts(rowIndex);
      }, 100);
    }
  };

  const calculateRowAmounts = (rowIndex) => {
    setEntries(prev => {
      const updated = [...prev];
      const entry = updated[rowIndex];
      if (!entry) return prev;
      
      const rbp = entry.rbp === 'Y';
      const qty = parseFloat(entry.qty) || 0;
      const rate = parseFloat(entry.rate) || 0;
      const pkg = parseFloat(entry.pkg || entry.dataPkg) || 0;
      
      // Calculate total amount
      let totalAmount = 0;
      if (rbp && pkg > 0) {
        totalAmount = pkg * qty * rate;
      } else {
        totalAmount = qty * rate;
      }
      
      // Calculate discount
      const percent = parseFloat(entry.percent) || 0;
      const discount = (totalAmount * percent) / 100;
      
      // Calculate net amount (debit or credit)
      const netAmount = totalAmount - discount;
      
      updated[rowIndex] = {
        ...entry,
        disc: discount,
        totalAmount
      };
      
      // Auto-set debit if both debit and credit are empty (not in edit mode with existing values)
      if (!isEditMode || (!entry.debit && !entry.credit)) {
        if (netAmount > 0) {
          updated[rowIndex].debit = netAmount;
          updated[rowIndex].credit = null;
        }
      }
      
      return updated;
    });
    updateTotals();
  };

  const updateTotals = () => {
    // This will be called to update the footer totals
    // The totals are calculated in the render
  };

  const addNewRow = async () => {
    const newIndex = entries.length;
    setEntries(prev => [...prev, {
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
    
    // Load categories for the new row if project is selected
    if (selectedProjectId) {
      const categories = await loadCategories(selectedProjectId, newIndex);
      setRowCategories(prev => ({
        ...prev,
        [newIndex]: categories
      }));
    }
  };

  const removeRow = (index) => {
    if (entries.length > 1) {
      setEntries(prev => prev.filter((_, i) => i !== index));
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
    setError('');
    setSuccess('');
    setValidationErrors([]);
    
    const errors = validateForm();
    if (errors.length > 0) {
      setValidationErrors(errors);
      return;
    }
    
    setLoading(true);
    
    try {
      const model = {
        projectId: selectedProjectId,
        entryDate: entryDate,
        dueDate: dueDate,
        creditDays: creditDays,
        bookerId: bookerId,
        requireQuantity: requireQuantity,
        isBaseVoucher: isBaseVoucher,
        saveAsRecipe: saveAsRecipe,
        recipeName: saveAsRecipe ? recipeName : null,
        entries: entries.map(entry => ({
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
        setSuccess(response.data.message);
        if (response.data.shouldPrint && response.data.printUrl) {
          window.open(response.data.printUrl, '_blank');
        }
        // Reset form or navigate
        if (!isEditMode) {
          setTimeout(() => {
            setEntries([{
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
            setSelectedProjectId(null);
            setBookerId(bookers.length > 0 ? bookers[0].id : null);
            setEntryDate(new Date().toISOString().split('T')[0]);
            setCreditDays(0);
            setSuccess('');
          }, 2000);
        }
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to save entries');
    } finally {
      setLoading(false);
    }
  };

  // Calculate totals
  const totalDebit = entries.reduce((sum, entry) => sum + (parseFloat(entry.debit) || 0), 0);
  const totalCredit = entries.reduce((sum, entry) => sum + (parseFloat(entry.credit) || 0), 0);
  
  // Check if base voucher checkbox should be shown
  const accountEntriesCount = entries.filter(e => e.accountGroup === 'account').length;
  const productEntriesCount = entries.filter(e => e.accountGroup === 'product').length;
  const showBaseVoucherCheckbox = accountEntriesCount === 1 && productEntriesCount >= 1;
  
  // Check if save recipe checkbox should be shown
  const allEntriesAreProducts = entries.every(e => !e.accountGroup || e.accountGroup === 'product');
  const hasAtLeastOneProduct = productEntriesCount > 0;
  const showSaveRecipeCheckbox = entries.length >= 2 && allEntriesAreProducts && hasAtLeastOneProduct;

  return (
    <div>
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
        <Alert variant="success" dismissible onClose={() => setSuccess('')}>
          <i className="fas fa-check-circle me-2"></i>{success}
        </Alert>
      )}

      {!hasBookers && (
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
        {/* Entry Details Card */}
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

        {/* Journal Entries Card */}
        <Card className="shadow mb-3">
          <Card.Header className="py-2 d-flex justify-content-between align-items-center">
            <h6 className="m-0 font-weight-bold text-primary">
              <i className="fas fa-list-alt me-2"></i>Journal Entries
            </h6>
            <Button variant="primary" size="sm" type="button" onClick={addNewRow}>
              <i className="fas fa-plus"></i> Add Row
            </Button>
          </Card.Header>
          <Card.Body className="p-0">
            <div className="table-responsive">
              <Table bordered className="align-middle mb-0">
                <thead className="table-primary">
                  <tr>
                    <th>Category <span className="text-danger">*</span></th>
                    <th>Account <span className="text-danger">*</span></th>
                    <th>PKG</th>
                    <th>RBP</th>
                    <th>Description</th>
                    <th>Qty <span className="text-danger" style={{ display: requireQuantity ? 'inline' : 'none' }}>*</span></th>
                    <th>Rate</th>
                    <th>%</th>
                    <th>Discount</th>
                    <th>Debit</th>
                    <th>Credit</th>
                    <th></th>
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
                                  accountType: null
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
                            value={entry.pkg || ''}
                            onChange={(e) => {
                              setEntries(prev => {
                                const updated = [...prev];
                                updated[index].pkg = e.target.value ? parseInt(e.target.value) : null;
                                return updated;
                              });
                              calculateRowAmounts(index);
                            }}
                            disabled={!isProductAccount}
                            readOnly
                          />
                        </td>
                        <td>
                          <Form.Select
                            size="sm"
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
                        </td>
                        <td>
                          <Form.Control
                            size="sm"
                            type="number"
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
                            min="0"
                            step="0.0001"
                            value={entry.rate || ''}
                            onChange={(e) => {
                              setEntries(prev => {
                                const updated = [...prev];
                                updated[index].rate = e.target.value ? parseFloat(e.target.value) : null;
                                return updated;
                              });
                              calculateRowAmounts(index);
                            }}
                            disabled={isAccountAccount}
                            onBlur={(e) => {
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
                            min="0"
                            max="100"
                            step="0.01"
                            value={entry.percent || 0}
                            onChange={(e) => {
                              setEntries(prev => {
                                const updated = [...prev];
                                updated[index].percent = e.target.value ? parseFloat(e.target.value) : 0;
                                return updated;
                              });
                              calculateRowAmounts(index);
                            }}
                            disabled={isAccountAccount}
                          />
                        </td>
                        <td>
                          <Form.Control
                            size="sm"
                            type="number"
                            min="0"
                            step="0.0001"
                            value={entry.disc ? entry.disc.toFixed(4) : '0.0000'}
                            readOnly
                            className="bg-light"
                            tabIndex="-1"
                          />
                        </td>
                        <td>
                          <Form.Control
                            size="sm"
                            type="number"
                            min="0"
                            step="0.0001"
                            value={entry.debit || ''}
                            onChange={(e) => {
                              setEntries(prev => {
                                const updated = [...prev];
                                updated[index].debit = e.target.value ? parseFloat(e.target.value) : null;
                                updated[index].credit = null; // Clear credit when debit is set
                                return updated;
                              });
                              updateTotals();
                            }}
                            onFocus={() => {
                              if (!entry.debit && !entry.credit) {
                                calculateRowAmounts(index);
                              }
                            }}
                          />
                        </td>
                        <td>
                          <Form.Control
                            size="sm"
                            type="number"
                            min="0"
                            step="0.0001"
                            value={entry.credit || ''}
                            onChange={(e) => {
                              setEntries(prev => {
                                const updated = [...prev];
                                updated[index].credit = e.target.value ? parseFloat(e.target.value) : null;
                                updated[index].debit = null; // Clear debit when credit is set
                                return updated;
                              });
                              updateTotals();
                            }}
                            onFocus={() => {
                              if (!entry.debit && !entry.credit) {
                                calculateRowAmounts(index);
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
                    <td className="fw-bold">{totalDebit.toFixed(4)}</td>
                    <td className="fw-bold">{totalCredit.toFixed(4)}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </Table>
            </div>
          </Card.Body>
        </Card>

        {/* Additional Details Card */}
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
                    min="0"
                    value={creditDays}
                    onChange={(e) => setCreditDays(parseInt(e.target.value) || 0)}
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

            {/* Require Quantity Checkbox */}
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

            {/* Base Voucher Checkbox */}
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

            {/* Save Recipe Checkbox */}
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

            {/* Recipe Name Input */}
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

        {/* Form Actions */}
        <div className="d-flex justify-content-between mb-3">
          <Button variant="secondary" type="button" onClick={() => navigate(-1)}>
            <i className="fas fa-arrow-left me-1"></i> Back
          </Button>
          <div>
            <Button
              variant="primary"
              type="submit"
              disabled={loading || !hasBookers || !bookerId}
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
              disabled={loading || !hasBookers || !bookerId}
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
