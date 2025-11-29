import React, { createContext, useState, useContext, useCallback } from 'react';
import apiClient from '../services/api';

const DataContext = createContext();

export function DataProvider({ children }) {
    // State for all our data - initialized as empty, loaded on demand
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [locations, setLocations] = useState([]);
    const [bookers, setBookers] = useState([]);
    
    // Loading states for each resource
    const [loadingStates, setLoadingStates] = useState({
        projects: false,
        categories: false,
        accounts: false,
        locations: false,
        bookers: false,
    });

    // Cache to track what's been loaded
    const [loaded, setLoaded] = useState({
        projects: false,
        categories: false,
        accounts: false,
        locations: false,
        bookers: false,
    });

    // Load projects only when needed
    const loadProjects = useCallback(async (force = false) => {
        if (loaded.projects && !force) return;
        setLoadingStates(prev => ({ ...prev, projects: true }));
        try {
            const response = await apiClient.get('/MetaDataApi/projects');
            if (response.data.success) {
                setProjects(response.data.data || []);
                setLoaded(prev => ({ ...prev, projects: true }));
            }
        } catch (error) {
            console.error('Error loading projects:', error);
            setProjects([]);
        } finally {
            setLoadingStates(prev => ({ ...prev, projects: false }));
        }
    }, [loaded.projects]);

    // Load categories only when needed
    const loadCategories = useCallback(async (force = false) => {
        if (loaded.categories && !force) return;
        setLoadingStates(prev => ({ ...prev, categories: true }));
        try {
            const response = await apiClient.get('/MetaDataApi/categories');
            if (response.data.success) {
                setCategories(response.data.data || []);
                setLoaded(prev => ({ ...prev, categories: true }));
            }
        } catch (error) {
            console.error('Error loading categories:', error);
            setCategories([]);
        } finally {
            setLoadingStates(prev => ({ ...prev, categories: false }));
        }
    }, [loaded.categories]);

    // Track loaded accounts by project/category combination
    const [loadedAccountsCache, setLoadedAccountsCache] = useState(new Set());

    // Load accounts only when needed (requires projectId)
    const loadAccounts = useCallback(async (projectId, categoryId = null, force = false) => {
        if (!projectId) {
            setAccounts([]);
            return;
        }
        
        const cacheKey = `${projectId}-${categoryId || 'all'}`;
        if (!force && loadedAccountsCache.has(cacheKey)) return;
        
        setLoadingStates(prev => ({ ...prev, accounts: true }));
        try {
            const params = { projectId };
            if (categoryId) params.categoryId = categoryId;
            
            const response = await apiClient.get('/MetaDataApi/accounts', { params });
            if (response.data.success) {
                setAccounts(response.data.data || []);
                setLoadedAccountsCache(prev => new Set([...prev, cacheKey]));
            }
        } catch (error) {
            console.error('Error loading accounts:', error);
            setAccounts([]);
        } finally {
            setLoadingStates(prev => ({ ...prev, accounts: false }));
        }
    }, [loadedAccountsCache]);

    // Load locations only when needed
    const loadLocations = useCallback(async (force = false) => {
        if (loaded.locations && !force) return;
        setLoadingStates(prev => ({ ...prev, locations: true }));
        try {
            const response = await apiClient.get('/MetaDataApi/locations');
            if (response.data.success) {
                setLocations(response.data.data || []);
                setLoaded(prev => ({ ...prev, locations: true }));
            }
        } catch (error) {
            console.error('Error loading locations:', error);
            setLocations([]);
        } finally {
            setLoadingStates(prev => ({ ...prev, locations: false }));
        }
    }, [loaded.locations]);

    // Load bookers only when needed
    const loadBookers = useCallback(async (force = false) => {
        if (loaded.bookers && !force) return;
        setLoadingStates(prev => ({ ...prev, bookers: true }));
        try {
            const response = await apiClient.get('/MetaDataApi/bookers');
            if (response.data.success) {
                setBookers(response.data.data || []);
                setLoaded(prev => ({ ...prev, bookers: true }));
            }
        } catch (error) {
            console.error('Error loading bookers:', error);
            setBookers([]);
        } finally {
            setLoadingStates(prev => ({ ...prev, bookers: false }));
        }
    }, [loaded.bookers]);

    // Load categories by project
    const loadCategoriesByProject = useCallback(async (projectId) => {
        if (!projectId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
            return [];
        } catch (error) {
            console.error('Error loading categories by project:', error);
            return [];
        }
    }, []);

    // Load accounts by category
    const loadAccountsByCategory = useCallback(async (categoryId) => {
        if (!categoryId) return [];
        try {
            const response = await apiClient.get(`/HomeApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                return response.data.data || [];
            }
            return [];
        } catch (error) {
            console.error('Error loading accounts by category:', error);
            return [];
        }
    }, []);

    // Project CRUD functions
    const addProject = async (project) => {
        try {
            const response = await apiClient.post('/MetaDataApi/projects', project);
            if (response.data.success) {
                await loadProjects(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to create project');
        } catch (error) {
            throw error;
        }
    };

    const updateProject = async (id, project) => {
        try {
            const response = await apiClient.put(`/MetaDataApi/projects/${id}`, project);
            if (response.data.success) {
                await loadProjects(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to update project');
        } catch (error) {
            throw error;
        }
    };

    const deleteProject = async (id) => {
        try {
            const response = await apiClient.delete(`/MetaDataApi/projects/${id}`);
            if (response.data.success) {
                await loadProjects(true);
            } else {
                throw new Error(response.data.message || 'Failed to delete project');
            }
        } catch (error) {
            throw error;
        }
    };

    // Category CRUD functions
    const addCategory = async (category) => {
        try {
            const response = await apiClient.post('/MetaDataApi/categories', category);
            if (response.data.success) {
                await loadCategories(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to create category');
        } catch (error) {
            throw error;
        }
    };

    const updateCategory = async (id, category) => {
        try {
            const response = await apiClient.put(`/MetaDataApi/categories/${id}`, category);
            if (response.data.success) {
                await loadCategories(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to update category');
        } catch (error) {
            throw error;
        }
    };

    const deleteCategory = async (id) => {
        try {
            const response = await apiClient.delete(`/MetaDataApi/categories/${id}`);
            if (response.data.success) {
                await loadCategories(true);
            } else {
                throw new Error(response.data.message || 'Failed to delete category');
            }
        } catch (error) {
            throw error;
        }
    };

    // Account CRUD functions
    const addAccount = async (account) => {
        try {
            const response = await apiClient.post('/MetaDataApi/accounts', account);
            if (response.data.success) {
                // Reload accounts if we have a projectId
                if (account.projectId) {
                    await loadAccounts(account.projectId, account.categoryId || null, true);
                }
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to create account');
        } catch (error) {
            throw error;
        }
    };

    const updateAccount = async (id, account) => {
        try {
            const response = await apiClient.put(`/MetaDataApi/accounts/${id}`, account);
            if (response.data.success) {
                // Clear cache and reload accounts if we have a projectId
                if (account.projectId) {
                    setLoadedAccountsCache(new Set());
                    await loadAccounts(account.projectId, account.categoryId || null, true);
                }
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to update account');
        } catch (error) {
            throw error;
        }
    };

    const deleteAccount = async (id, projectId = null, categoryId = null) => {
        try {
            const response = await apiClient.delete(`/MetaDataApi/accounts/${id}`);
            if (response.data.success) {
                // Clear cache and reload if projectId provided
                if (projectId) {
                    setLoadedAccountsCache(new Set());
                    await loadAccounts(projectId, categoryId, true);
                }
            } else {
                throw new Error(response.data.message || 'Failed to delete account');
            }
        } catch (error) {
            throw error;
        }
    };

    // Location CRUD functions
    const addLocation = async (location) => {
        try {
            const response = await apiClient.post('/MetaDataApi/locations', location);
            if (response.data.success) {
                await loadLocations(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to create location');
        } catch (error) {
            throw error;
        }
    };

    const updateLocation = async (id, location) => {
        try {
            const response = await apiClient.put(`/MetaDataApi/locations/${id}`, location);
            if (response.data.success) {
                await loadLocations(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to update location');
        } catch (error) {
            throw error;
        }
    };

    const deleteLocation = async (id) => {
        try {
            const response = await apiClient.delete(`/MetaDataApi/locations/${id}`);
            if (response.data.success) {
                await loadLocations(true);
            } else {
                throw new Error(response.data.message || 'Failed to delete location');
            }
        } catch (error) {
            throw error;
        }
    };

    // Booker CRUD functions
    const addBooker = async (booker) => {
        try {
            const response = await apiClient.post('/MetaDataApi/bookers', booker);
            if (response.data.success) {
                await loadBookers(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to create booker');
        } catch (error) {
            throw error;
        }
    };

    const updateBooker = async (id, booker) => {
        try {
            const response = await apiClient.put(`/MetaDataApi/bookers/${id}`, booker);
            if (response.data.success) {
                await loadBookers(true);
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to update booker');
        } catch (error) {
            throw error;
        }
    };

    const deleteBooker = async (id) => {
        try {
            const response = await apiClient.delete(`/MetaDataApi/bookers/${id}`);
            if (response.data.success) {
                await loadBookers(true);
            } else {
                throw new Error(response.data.message || 'Failed to delete booker');
            }
        } catch (error) {
            throw error;
        }
    };

    return (
        <DataContext.Provider value={{
            // Data
            projects,
            categories,
            accounts,
            locations,
            bookers,
            
            // Loading states
            loadingStates,
            
            // Load functions (on-demand loading)
            loadProjects,
            loadCategories,
            loadAccounts,
            loadLocations,
            loadBookers,
            loadCategoriesByProject,
            loadAccountsByCategory,
            
            // CRUD functions
            addProject,
            updateProject,
            deleteProject,
            addCategory,
            updateCategory,
            deleteCategory,
            addAccount,
            updateAccount,
            deleteAccount,
            addLocation,
            updateLocation,
            deleteLocation,
            addBooker,
            updateBooker,
            deleteBooker,
        }}>
            {children}
        </DataContext.Provider>
    );
}

export function useData() {
    const context = useContext(DataContext);
    if (!context) {
        throw new Error('useData must be used within a DataProvider');
    }
    return context;
}

