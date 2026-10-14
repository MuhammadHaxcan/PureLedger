import apiClient from './api';

const projectImgApi = {
    get: projectId => apiClient.get(`/ProjectImgApi/${projectId}`),
    save: (projectId, imageDataUrl) => apiClient.put(`/ProjectImgApi/${projectId}`, { imageDataUrl }),
    remove: projectId => apiClient.delete(`/ProjectImgApi/${projectId}`),
};

export const getProjectImageDataUrl = async projectId => {
    if (!projectId) return null;
    const response = await projectImgApi.get(projectId);
    return response.data?.data?.imageDataUrl || null;
};

export default projectImgApi;
