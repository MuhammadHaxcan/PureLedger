import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import Modal from '../../components/Modal';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const Recipes = () => {
    usePageTitle('Recipes');
    const [projects, setProjects] = useState([]);
    const [selectedProjectId, setSelectedProjectId] = useState('');
    const [recipes, setRecipes] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [showItemsModal, setShowItemsModal] = useState(false);
    const [selectedRecipe, setSelectedRecipe] = useState(null);
    const [recipeItems, setRecipeItems] = useState([]);

    useEffect(() => {
        loadProjects();
    }, []);

    useEffect(() => {
        if (selectedProjectId) {
            loadRecipes(Number(selectedProjectId));
        } else {
            setRecipes([]);
        }
    }, [selectedProjectId]);

    const loadProjects = async () => {
        try {
            const response = await apiClient.get('/HomeApi/entry-data');
            if (response.data.success && response.data.projects) {
                setProjects(response.data.projects);
            }
        } catch (err) {
            setError('Failed to load projects');
        } finally {
            setLoading(false);
        }
    };

    const loadRecipes = async (projectId) => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get(`/HomeApi/recipes?projectId=${projectId}`);
            if (response.data.success) {
                setRecipes(response.data.data?.recipes || response.data.data?.Recipes || []);
            } else {
                setError('Failed to load recipes');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load recipes');
        } finally {
            setLoading(false);
        }
    };

    const handleViewItems = async (recipe) => {
        setSelectedRecipe(recipe);
        setLoading(true);
        try {
            const response = await apiClient.get(`/HomeApi/recipe-items/${recipe.recipeId}`);
            if (response.data.success) {
                setRecipeItems(response.data.data?.items || []);
                setShowItemsModal(true);
            } else {
                setError('Failed to load recipe items');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load recipe items');
        } finally {
            setLoading(false);
        }
    };

    if (loading && !recipes.length) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="card shadow mb-4">
                <div className="card-header py-3">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-book-open me-2"></i>Recipes
                    </h6>
                </div>
                <div className="card-body">
                    <div className="row mb-4">
                        <div className="col-md-6">
                            <label htmlFor="projectSelect" className="form-label">Select Project:</label>
                            <InputGroup>
                                <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                <SearchableSelect
                                    value={selectedProjectId ? Number(selectedProjectId) : null}
                                    onChange={(value) => setSelectedProjectId(value ? String(value) : '')}
                                    options={projects.map(p => ({ 
                                        id: p.id || p.projectId, 
                                        name: p.name || p.projectName 
                                    }))}
                                    placeholder="Search project..."
                                />
                            </InputGroup>
                        </div>
                    </div>

                    {error && (
                        <div className="alert alert-danger alert-dismissible fade show" role="alert">
                            <i className="fas fa-exclamation-circle me-2"></i>{error}
                            <button type="button" className="btn-close" onClick={() => setError('')}></button>
                        </div>
                    )}

                    <div className="table-responsive">
                        <table className="table table-bordered table-striped">
                            <thead className="table-primary">
                                <tr>
                                    <th>ID</th>
                                    <th>Recipe Name</th>
                                    <th>Project</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {recipes.length > 0 ? (
                                    recipes.map((recipe) => (
                                        <tr key={recipe.recipeId}>
                                            <td>{recipe.recipeId}</td>
                                            <td>{recipe.recipeName}</td>
                                            <td>{recipe.projectName}</td>
                                            <td>
                                                <button
                                                    type="button"
                                                    className="btn btn-info btn-sm me-2"
                                                    onClick={() => handleViewItems(recipe)}
                                                >
                                                    <i className="fas fa-eye"></i> View Items
                                                </button>
                                                <Link
                                                    to={`/home/create-from-recipe/${recipe.recipeId}/${selectedProjectId}`}
                                                    className="btn btn-success btn-sm"
                                                >
                                                    <i className="fas fa-plus"></i> Create Entry
                                                </Link>
                                            </td>
                                        </tr>
                                    ))
                                ) : selectedProjectId ? (
                                    <tr>
                                        <td colSpan="4" className="text-center">No recipes found for this project.</td>
                                    </tr>
                                ) : (
                                    <tr>
                                        <td colSpan="4" className="text-center">Please select a project to view recipes.</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

            <Modal
                show={showItemsModal}
                onClose={() => setShowItemsModal(false)}
                title={`Recipe Items - ${selectedRecipe?.recipeName}`}
                size="lg"
            >
                <div className="recipe-details mb-3">
                    <h5>{selectedRecipe?.recipeName}</h5>
                    <p className="text-muted">{selectedRecipe?.projectName}</p>
                </div>
                <div className="table-responsive">
                    <table className="table table-bordered table-striped">
                        <thead className="table-light">
                            <tr>
                                <th>ID</th>
                                <th>Category</th>
                                <th>Account</th>
                            </tr>
                        </thead>
                        <tbody>
                            {recipeItems.length > 0 ? (
                                recipeItems.map((item, index) => (
                                    <tr key={index}>
                                        <td>{item.recipeItemId}</td>
                                        <td>{item.categoryName}</td>
                                        <td>{item.accountName}</td>
                                    </tr>
                                ))
                            ) : (
                                <tr>
                                    <td colSpan="3" className="text-center">No items found</td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </Modal>
        </div>
    );
};

export default Recipes;

