import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import Modal from './Modal';
import ConfirmModal from './ConfirmModal';

describe('Dialog error display', () => {
    it('Modal shows the error inside the dialog and lets the user dismiss it', async () => {
        const onErrorDismiss = vi.fn();
        render(
            <Modal show onHide={() => {}} title="New thing" error="Name is already used" onErrorDismiss={onErrorDismiss}>
                <p>form body</p>
            </Modal>
        );

        const dialog = screen.getByRole('dialog');
        expect(within(dialog).getByText('Name is already used')).toBeInTheDocument();
        await userEvent.click(within(dialog).getByRole('button', { name: /close alert/i }));
        expect(onErrorDismiss).toHaveBeenCalledOnce();
    });

    it('Modal shows no alert when there is no error', () => {
        render(<Modal show onHide={() => {}} title="New thing"><p>form body</p></Modal>);

        expect(within(screen.getByRole('dialog')).queryByRole('alert')).not.toBeInTheDocument();
    });

    it('ConfirmModal shows a failed confirm action inside the dialog instead of behind it', () => {
        render(
            <ConfirmModal
                show
                onHide={() => {}}
                onConfirm={() => {}}
                title="Delete"
                message="Are you sure?"
                error="This entry cannot be deleted"
                onErrorDismiss={() => {}}
            />
        );

        const dialog = screen.getByRole('dialog');
        expect(within(dialog).getByText('This entry cannot be deleted')).toBeInTheDocument();
        expect(within(dialog).getByText('Are you sure?')).toBeInTheDocument();
    });
});
