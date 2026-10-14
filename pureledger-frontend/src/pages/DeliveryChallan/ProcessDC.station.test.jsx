import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProcessDC from './ProcessDC';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({ default: { get: vi.fn(), post: vi.fn() } }));
vi.mock('../../hooks/usePageTitle', () => ({ usePageTitle: vi.fn() }));
vi.mock('../../components/SearchableSelect', () => ({ default: ({ placeholder, options, value, onChange, disabled }) => (
  <select aria-label={placeholder} value={value ?? ''} disabled={disabled} onChange={e => onChange(Number(e.target.value) || null)}>
    <option value="">{placeholder}</option>
    {options.map(option => <option key={option.id} value={option.id}>{option.name}</option>)}
  </select>
) }));

const entry = (id, description) => ({ entryId: id, accountName: 'Product', description, quantity: 10 });
const vouchers = [
  { jvId: 101, entryDate: '2026-10-10', entries: [entry(1, 'Entry A'), entry(2, 'Entry B')] },
  { jvId: 102, entryDate: '2026-10-10', entries: [entry(3, 'Entry C')] },
];
const reply = station => ({ data: { success: true, exists: station != null, challanId: 7,
  challan: station == null ? null : { station, locationId: 1, challanDate: '2026-10-10' } } });
const deferred = () => {
  let resolve, reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
};
let savedStation;
let lookup;

async function setup() {
  const user = userEvent.setup();
  const rendered = render(<ProcessDC />);
  await screen.findAllByText('Station project');
  const panel = screen.getByRole('tabpanel', { name: 'Process Delivery Challan' });
  await user.selectOptions(within(panel).getByRole('combobox', { name: 'Select Project' }), '1');
  await user.click(within(panel).getByRole('button', { name: 'Generate Report' }));
  await screen.findByText('Entry A');
  return { user, ...rendered };
}
async function open(user, description = 'Entry A') {
  await user.click(within(screen.getByText(description).closest('tr')).getByRole('button', { name: 'Process' }));
  return screen.findByLabelText('Station');
}

describe('shared delivery challan station', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    savedStation = null;
    lookup = () => Promise.resolve(reply(savedStation));
    vi.spyOn(window, 'open').mockImplementation(() => null);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({}));
    apiClient.get.mockImplementation((url) => {
      if (url.includes('check-existing-challan')) return lookup(url);
      if (url.endsWith('process-dc-data')) return Promise.resolve({ data: { success: true, projects: [{ id: 1, name: 'Station project' }] } });
      if (url.endsWith('filtered-entries')) return Promise.resolve({ data: { success: true, data: { vouchers } } });
      if (url.endsWith('locations')) return Promise.resolve({ data: { success: true, data: [{ id: 1, name: 'Warehouse' }] } });
      if (url.endsWith('check-for-charges-available')) return Promise.resolve({ data: { success: true, available: false } });
      if (url.endsWith('transporter-category-id')) return Promise.resolve({ data: { success: true, value: 9 } });
      if (url.endsWith('accounts-by-category/9')) return Promise.resolve({ data: { success: true, data: [{ id: 4, name: 'Transporter' }] } });
      return Promise.resolve({ data: { success: true, data: [] } });
    });
    apiClient.post.mockImplementation((url, payload) => {
      savedStation = (payload.Station ?? payload.dcStation)?.trim() || '';
      return Promise.resolve({ data: { success: true, data: { journalVoucherId: 101 } } });
    });
  });

  it('prefills another entry after saving, persists after remount, and cancels edits', async () => {
    let { user, unmount } = await setup();
    const station = await open(user);
    await waitFor(() => expect(station).toBeEnabled());
    expect(station).toHaveAttribute('maxlength', '250');
    await user.type(station, 'Lahore');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Select Location' }), '1');
    await user.click(screen.getByRole('button', { name: 'Post Single' }));
    await waitFor(() => expect(screen.queryByLabelText('Station')).not.toBeInTheDocument());
    expect(await open(user, 'Entry B')).toHaveValue('Lahore');
    await user.clear(screen.getByLabelText('Station'));
    await user.type(screen.getByLabelText('Station'), 'Karachi');
    await user.click(screen.getByRole('button', { name: 'Post Single' }));
    await waitFor(() => expect(screen.queryByLabelText('Station')).not.toBeInTheDocument());
    unmount();
    ({ user, unmount } = await setup());
    expect(await open(user, 'Entry B')).toHaveValue('Karachi');
    await user.clear(screen.getByLabelText('Station'));
    await user.type(screen.getByLabelText('Station'), 'Unsaved');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByLabelText('Station')).not.toBeInTheDocument());
    expect(await open(user, 'Entry A')).toHaveValue('Karachi');
    expect(apiClient.post).toHaveBeenCalledTimes(2);
    unmount();
    // Types four station values and mounts twice; it needs more than the 5 s default when the suite runs in parallel.
  }, 20000);

  it('blocks Station and all processing actions during lookup, then prefills', async () => {
    const pending = deferred();
    lookup = () => pending.promise;
    const { user } = await setup();
    const station = await open(user);
    expect(station).toBeDisabled();
    for (const name of ['Post Single', 'Post All', 'Add Cartage']) expect(screen.getByRole('button', { name })).toBeDisabled();
    await act(async () => pending.resolve(reply('Lahore')));
    expect(station).toHaveValue('Lahore');
    expect(station).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Post Single' })).toBeEnabled();
  });

  it.each(['network', 'malformed'])('blocks processing after a %s lookup failure until Retry succeeds', async failure => {
    lookup = () => failure === 'network' ? Promise.reject(new Error('offline')) : Promise.resolve({ data: { success: false } });
    const { user } = await setup();
    const station = await open(user);
    await screen.findByRole('button', { name: 'Retry' });
    expect(station).toBeDisabled();
    for (const name of ['Post Single', 'Post All', 'Add Cartage']) expect(screen.getByRole('button', { name })).toBeDisabled();
    lookup = () => Promise.resolve(reply('Karachi'));
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(station).toHaveValue('Karachi'));
    expect(station).toBeEnabled();
    expect(apiClient.post).not.toHaveBeenCalled();
  });

  it.each(['resolve', 'reject'])('ignores a closed voucher’s delayed %s response after another voucher opens', async result => {
    const old = deferred();
    lookup = url => url.endsWith('/101') ? old.promise : Promise.resolve(reply('Karachi'));
    const { user } = await setup();
    await open(user);
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByLabelText('Station')).not.toBeInTheDocument());
    const station = await open(user, 'Entry C');
    await waitFor(() => expect(station).toHaveValue('Karachi'));
    await act(async () => result === 'resolve' ? old.resolve(reply('Lahore')) : old.reject(new Error('old error')));
    expect(station).toHaveValue('Karachi');
    expect(station).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument();
  });

  it('resets Station to blank while switching to a different voucher without a saved station', async () => {
    lookup = url => Promise.resolve(reply(url.endsWith('/101') ? 'Lahore' : null));
    const { user } = await setup();
    expect(await open(user)).toHaveValue('Lahore');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(screen.queryByLabelText('Station')).not.toBeInTheDocument());
    expect(await open(user, 'Entry C')).toHaveValue('');
  });

  it.each([['Post Single', 'single-entry'], ['Post All', 'process-all'], ['Add Cartage', 'add-cartage']])(
    'sends editable Station through %s', async (action, route) => {
      savedStation = 'Lahore';
      const { user } = await setup();
      const station = await open(user);
      await waitFor(() => expect(station).toBeEnabled());
      await user.clear(station);
      await user.type(station, 'Karachi');
      await user.click(screen.getByRole('button', { name: action }));
      if (action === 'Add Cartage') {
        await user.selectOptions(await screen.findByRole('combobox', { name: 'Select Transporter' }), '4');
        await user.type(screen.getByPlaceholderText('Enter amount'), '500');
        const dialog = screen.getByPlaceholderText('Enter amount').closest('[role="dialog"]');
        await user.click(within(dialog).getByRole('button', { name: 'Add Cartage & Process' }));
      }
      await waitFor(() => expect(apiClient.post).toHaveBeenCalledWith(`/DeliveryChallanApi/${route}`,
        expect.objectContaining({ [action === 'Add Cartage' ? 'dcStation' : 'Station']: 'Karachi' })));
    });
});

