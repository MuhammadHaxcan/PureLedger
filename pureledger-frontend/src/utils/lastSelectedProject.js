export const LAST_PROJECT_STORAGE_KEY = 'pl_last_selected_project';

const projectIdOf = project => project?.id ?? project?.projectId;

export const rememberLastSelectedProject = projectId => {
    if (projectId !== null && projectId !== undefined && String(projectId) !== '') {
        localStorage.setItem(LAST_PROJECT_STORAGE_KEY, String(projectId));
    }
};

export const resolveLastSelectedProject = projects => {
    const availableProjects = Array.isArray(projects) ? projects : [];
    const rememberedProjectId = localStorage.getItem(LAST_PROJECT_STORAGE_KEY);
    const rememberedProject = rememberedProjectId
        ? availableProjects.find(project => String(projectIdOf(project)) === rememberedProjectId)
        : null;
    const selectedProject = rememberedProject ?? availableProjects[0];
    const selectedProjectId = projectIdOf(selectedProject);

    return selectedProjectId === null || selectedProjectId === undefined
        ? ''
        : String(selectedProjectId);
};
