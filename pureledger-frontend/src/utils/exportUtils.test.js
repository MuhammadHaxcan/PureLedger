import { beforeEach, describe, expect, it, vi } from 'vitest';
import { generatePdfAsync } from './exportUtils';

const mocks = vi.hoisted(() => {
    const document = {
        internal: { pageSize: { width: 210, height: 297 }, getNumberOfPages: () => 3 },
        setFontSize: vi.fn(), setTextColor: vi.fn(), text: vi.fn(),
        getImageProperties: vi.fn(() => ({ width: 200, height: 100, fileType: 'PNG' })),
        addImage: vi.fn(), setPage: vi.fn(), save: vi.fn(),
    };
    return { document, autoTable: vi.fn() };
});

vi.mock('jspdf', () => ({ jsPDF: class { constructor() { return mocks.document; } } }));
vi.mock('jspdf-autotable', () => ({ default: mocks.autoTable }));

describe('report PDF project logo', () => {
    beforeEach(() => vi.clearAllMocks());

    const config = {
        title: 'Account Report', projectName: 'North',
        generatedInfo: 'Generated now', columns: [{ header: 'ID', dataKey: 'id' }],
        rows: [{ id: 1 }], startY: 40, filename: 'report.pdf',
    };

    it('adds the logo once on the first page without covering the table', async () => {
        await generatePdfAsync({ ...config, logoDataUrl: 'data:image/png;base64,iVBORw0KGgo=' });

        expect(mocks.document.addImage).toHaveBeenCalledTimes(1);
        const [, , x, y, width, height] = mocks.document.addImage.mock.calls[0];
        expect(x).toBeGreaterThan(14);
        expect(y).toBe(10);
        expect(width).toBeLessThanOrEqual(34);
        expect(height).toBeLessThanOrEqual(22);
        expect(mocks.autoTable.mock.calls[0][1].startY).toBeGreaterThan(y + height);
        expect(mocks.document.setPage).toHaveBeenCalledTimes(3);
    });

    it('keeps a text-only header when the project has no logo', async () => {
        await generatePdfAsync(config);
        expect(mocks.document.addImage).not.toHaveBeenCalled();
        expect(mocks.document.save).toHaveBeenCalledWith('report.pdf');
    });
});
