import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Projects from './Projects';
import apiClient from '../../services/api';
import projectImgApi from '../../services/projectImgApi';

vi.mock('../../services/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  fileToBase64: vi.fn(async () => 'data:image/png;base64,iVBORw0KGgo='),
}));
vi.mock('../../services/projectImgApi', () => ({
  default: { get: vi.fn(), save: vi.fn(), remove: vi.fn() },
}));

describe('Projects logo management', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiClient.get.mockResolvedValue({ data: { success: true, data: [{ projectId: 7, projectName: 'North' }] } });
    apiClient.post.mockResolvedValue({ data: { success: true, message: 'Created', data: { projectId: 8, projectName: 'South' } } });
    apiClient.put.mockResolvedValue({ data: { success: true, message: 'Updated', data: { projectId: 7, projectName: 'North' } } });
    projectImgApi.get.mockResolvedValue({ data: { data: { imageDataUrl: 'data:image/png;base64,iVBORw0KGgo=' } } });
    projectImgApi.save.mockResolvedValue({ data: { success: true } });
    projectImgApi.remove.mockResolvedValue({ data: { success: true } });
  });

  it('creates a project and uploads its logo after receiving the project ID', async () => {
    const user = userEvent.setup();
    render(<Projects />);
    await user.click(await screen.findByRole('button', { name: 'New Project' }));
    await user.type(screen.getByLabelText('Project Name'), 'South');
    await user.upload(screen.getByLabelText('Project Logo'), new File(['png'], 'logo.png', { type: 'image/png' }));
    await screen.findByAltText('Project logo preview');
    await user.click(screen.getByRole('button', { name: 'Create' }));

    await waitFor(() => expect(projectImgApi.save).toHaveBeenCalledWith(8, 'data:image/png;base64,iVBORw0KGgo='));
  });

  it('removes only the selected project logo when updating it', async () => {
    const user = userEvent.setup();
    render(<Projects />);
    await screen.findByText('North');
    await user.click(screen.getByRole('button', { name: 'Edit project North' }));
    await screen.findByAltText('Project logo preview');
    await user.click(screen.getByRole('button', { name: 'Remove logo' }));
    await user.click(screen.getByRole('button', { name: 'Update' }));

    await waitFor(() => expect(projectImgApi.remove).toHaveBeenCalledWith(7));
    expect(projectImgApi.save).not.toHaveBeenCalled();
  });

  it('keeps the modal open if the logo upload fails after project creation', async () => {
    projectImgApi.save.mockRejectedValue(new Error('network error'));
    const user = userEvent.setup();
    render(<Projects />);
    await user.click(await screen.findByRole('button', { name: 'New Project' }));
    await user.type(screen.getByLabelText('Project Name'), 'South');
    await user.upload(screen.getByLabelText('Project Logo'), new File(['png'], 'logo.png', { type: 'image/png' }));
    await screen.findByAltText('Project logo preview');
    await user.click(screen.getByRole('button', { name: 'Create' }));

    expect(await screen.findByText(/Project saved, but logo update failed/)).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Update' })).toBeInTheDocument();
  });
});
