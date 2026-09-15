import { beforeEach, describe, expect, it, vi } from 'vitest';
import apiClient from './api';
import approvalApi from './approvalApi';

vi.mock('./api', () => ({
    default: { get: vi.fn(), put: vi.fn(), post: vi.fn() },
}));

describe('approvalApi P&R concurrency payloads', () => {
    beforeEach(() => vi.clearAllMocks());

    it('sends the reviewed P&R snapshot when approving', () => {
        approvalApi.approve(12, '66ea1753-067a-4fd3-a7a9-16ef5fcf86e1');
        expect(apiClient.post).toHaveBeenCalledWith('/ApprovalApi/12/approve', {
            snapshotVersion: '66ea1753-067a-4fd3-a7a9-16ef5fcf86e1',
        });
    });

    it('keeps the Expense rejection body unchanged when no snapshot is supplied', () => {
        approvalApi.reject(13, 'Not supported');
        expect(apiClient.post).toHaveBeenCalledWith('/ApprovalApi/13/reject', {
            reason: 'Not supported',
        });
    });
});
