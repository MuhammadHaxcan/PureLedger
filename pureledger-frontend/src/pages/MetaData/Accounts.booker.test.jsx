import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Accounts from './Accounts';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

const bookers = [{ bookerID: 11, bookerName: 'Salesperson A', isEnabled: true },
  { bookerID: 12, bookerName: 'Salesperson B', isEnabled: true },
  { bookerID: 13, bookerName: 'Former salesperson', isEnabled: false }];
const select = (placeholder, value) => fireEvent.change(screen.getAllByPlaceholderText(placeholder).at(-1).parentElement.querySelector('select'), { target: { value } });
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };
let saved;
let bookerLookup;

beforeEach(() => {
  vi.clearAllMocks();
  saved = { accountId: 100, accountName: 'Customer A', categoryId: 10, accountGroupId: 1,
    linkedBookerId: 11, linkedBookerName: 'Salesperson A', isEnabled: true, accountData: {} };
  bookerLookup = () => Promise.resolve({ data: { success: true, data: bookers } });
  apiClient.get.mockImplementation(async url => {
    let data;
    if (url === '/MetaDataApi/projects-for-accounts') data = [{ projectId: 1, projectName: 'Main' }];
    else if (url === '/MetaDataApi/account-groups') data = [{ id: 1, name: 'Account' }, { id: 2, name: 'Product' }, { id: 3, name: 'Expense' }];
    else if (url === '/MetaDataApi/categories/by-project/1') data = [{ id: 10, name: 'Customers' }];
    else if (url === '/MetaDataApi/categories/10') data = { projectId: 1 };
    else if (url === '/MetaDataApi/accounts') data = [saved, { ...saved, accountId: 200, accountName: 'Customer B', linkedBookerId: null, linkedBookerName: null }];
    else if (url === '/MetaDataApi/accounts/100') return { data: { success: true, data: saved, hasJournalAssociations: true } };
    else if (url === '/MetaDataApi/accounts/200') return { data: { success: true, data: { ...saved, accountId: 200, accountName: 'Customer B', linkedBookerId: null }, hasJournalAssociations: true } };
    else if (url === '/MetaDataApi/bookers') return bookerLookup();
    else throw new Error(`Unexpected GET ${url}`);
    return { data: { success: true, data } };
  });
  apiClient.put.mockImplementation(async (_url, payload) => {
    saved = { ...saved, ...payload, linkedBookerName: bookers.find(b => b.bookerID === payload.linkedBookerId)?.bookerName || null };
    return { data: { success: true, message: 'Account updated' } };
  });
  apiClient.post.mockResolvedValue({ data: { success: true, message: 'Account created' } });
});

const chooseProject = async () => {
  await waitFor(() => expect(screen.getAllByPlaceholderText('Select Project').at(-1).parentElement.querySelector('option[value="1"]')).toBeInTheDocument());
  select('Select Project', '1');
};

const openEdit = async () => {
  await chooseProject();
  fireEvent.click(await screen.findByRole('button', { name: 'Edit Customer A' }));
  await screen.findByRole('button', { name: 'Update' });
  await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Salesperson A'));
};

describe('Account linked booker', () => {
  it('creates with an optional assignment and clears it when changing to Product', async () => {
    render(<Accounts />);
    await screen.findByPlaceholderText('Select Project');
    fireEvent.click(screen.getByRole('button', { name: 'New Account' }));
    await chooseProject();
    await waitFor(() => expect(screen.getByPlaceholderText('Select Category').parentElement.querySelector('option[value="10"]')).toBeInTheDocument());
    select('Select Category', '10'); select('Select Account Group', '1');
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).not.toBeDisabled());
    expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Unassigned');
    select('Search linked booker...', '11');
    fireEvent.click(screen.getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(apiClient.post).toHaveBeenCalledWith('/MetaDataApi/accounts', expect.objectContaining({ linkedBookerId: 11 })));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'New Account' }));
    select('Select Account Group', '1');
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).not.toBeDisabled());
    select('Search linked booker...', '11'); select('Select Account Group', '2');
    expect(screen.queryByLabelText('Linked Booker / Salesperson')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(apiClient.post).toHaveBeenLastCalledWith('/MetaDataApi/accounts', expect.objectContaining({ linkedBookerId: null, accountGroupId: 2 })));
  });

  it('prefills edits, saves reassignment, reloads after refresh, and explicitly clears', async () => {
    let view = render(<Accounts />); await openEdit();
    select('Search linked booker...', '12'); fireEvent.click(screen.getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(apiClient.put).toHaveBeenCalledWith('/MetaDataApi/accounts/100', expect.objectContaining({ linkedBookerId: 12 })));
    view.unmount(); view = render(<Accounts />);
    await chooseProject(); fireEvent.click(await screen.findByRole('button', { name: 'Edit Customer A' }));
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Salesperson B'));
    select('Search linked booker...', ''); fireEvent.click(screen.getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(apiClient.put).toHaveBeenLastCalledWith('/MetaDataApi/accounts/100', expect.objectContaining({ linkedBookerId: null })));
  });

  it('cancels without saving and resets assignment when editing another account', async () => {
    render(<Accounts />); await openEdit();
    select('Search linked booker...', '12'); fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(apiClient.put).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Edit Customer B' }));
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Unassigned'));
  });

  it('retains the saved disabled booker but hides disabled bookers for new assignments', async () => {
    saved = { ...saved, linkedBookerId: 13, linkedBookerName: 'Former salesperson' };
    render(<Accounts />); await chooseProject();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Customer A' }));
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Former salesperson (Disabled)'));
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'New Account' })); select('Select Account Group', '1');
    await waitFor(() => expect(screen.getByLabelText('Linked Booker / Salesperson')).not.toBeDisabled());
    expect(screen.getByLabelText('Linked Booker / Salesperson').parentElement.querySelector('option[value="13"]')).not.toBeInTheDocument();
  });

  it('blocks saving on lookup failure and enables it after Retry', async () => {
    bookerLookup = () => Promise.reject(new Error('offline'));
    render(<Accounts />); await chooseProject(); fireEvent.click(await screen.findByRole('button', { name: 'Edit Customer A' }));
    await screen.findByRole('button', { name: 'Retry bookers' });
    expect(screen.getByRole('button', { name: 'Update' })).toBeDisabled();
    bookerLookup = () => Promise.resolve({ data: { success: true, data: bookers } });
    fireEvent.click(screen.getByRole('button', { name: 'Retry bookers' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Update' })).not.toBeDisabled());
    expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Salesperson A');
  });

  it('ignores an old modal lookup after closing and opening another account', async () => {
    const old = deferred(); bookerLookup = () => old.promise;
    render(<Accounts />); await chooseProject(); fireEvent.click(await screen.findByRole('button', { name: 'Edit Customer A' }));
    await screen.findByRole('button', { name: 'Update' });
    expect(screen.getByRole('button', { name: 'Update' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    bookerLookup = () => Promise.resolve({ data: { success: true, data: bookers } });
    fireEvent.click(screen.getByRole('button', { name: 'Edit Customer B' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Update' })).not.toBeDisabled());
    await act(async () => old.resolve({ data: { success: true, data: [] } }));
    expect(screen.getByLabelText('Linked Booker / Salesperson').parentElement.querySelector('option[value="11"]')).toBeInTheDocument();
    expect(screen.getByLabelText('Linked Booker / Salesperson')).toHaveValue('Unassigned');
  });
});
