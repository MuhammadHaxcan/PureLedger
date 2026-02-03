import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import Papa from 'papaparse';

const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms));

export const exportToCsvAsync = async (data, filename, onProgress) => {
    if (!data || data.length === 0) {
        alert('No data to export');
        return;
    }

    if (onProgress) onProgress(0.5);
    
    await delay(10);
    
    const csvContent = Papa.unparse(data);
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    
    if (onProgress) onProgress(1);
};

export const generatePdfAsync = async (config, onProgress) => {
    const {
        orientation = 'portrait',
        format = 'a4',
        title,
        subtitle,
        projectName,
        dateRange,
        generatedInfo,
        columns,
        rows,
        rowsPerPage = 14,
        startY = 46,
        fontSize = 9,
        cellPadding = 2,
        filename
    } = config;

    const doc = new jsPDF(orientation, 'mm', format);
    
    if (onProgress) onProgress(0.1);
    await delay(10);

    if (title) {
        doc.setFontSize(18);
        doc.setTextColor(30, 64, 175);
        doc.text(title, 14, 20);
    }

    if (onProgress) onProgress(0.2);
    await delay(10);

    if (projectName || dateRange || generatedInfo || subtitle) {
        doc.setFontSize(10);
        doc.setTextColor(100);
        let yPos = 28;
        
        if (projectName) {
            doc.text(`Project: ${projectName}`, 14, yPos);
            yPos += 6;
        }
        
        if (dateRange) {
            doc.text(`Date Range: ${dateRange}`, 14, yPos);
            yPos += 6;
        }
        
        if (subtitle) {
            const subtitleLines = subtitle.split('\n');
            subtitleLines.forEach(line => {
                if (line.trim()) {
                    doc.text(line.trim(), 14, yPos);
                    yPos += 6;
                }
            });
        }
        
        if (generatedInfo) {
            doc.text(generatedInfo, 14, yPos);
            yPos += 6;
        }
    }

    if (onProgress) onProgress(0.3);
    await delay(10);

    const totalRows = rows.length;
    const totalPages = Math.ceil(totalRows / rowsPerPage);
    let currentStartY = startY;

    for (let i = 0; i < rows.length; i += rowsPerPage) {
        if (i > 0) {
            doc.addPage();
            currentStartY = 20;
        }

        const chunk = rows.slice(i, i + rowsPerPage);
        
        autoTable(doc, {
            columns,
            body: chunk,
            startY: currentStartY,
            styles: { fontSize, cellPadding },
            headStyles: { fillColor: [241, 245, 249], textColor: [30, 41, 59], fontStyle: 'bold' },
            footStyles: { fillColor: [241, 245, 249], fontStyle: 'bold' }
        });

        const progress = 0.3 + (0.5 * (i + rowsPerPage) / totalRows);
        if (onProgress) onProgress(Math.min(progress, 0.8));
        await delay(10);
    }

    if (onProgress) onProgress(0.9);
    await delay(10);

    const pageCount = doc.internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
        doc.setPage(i);
        doc.setFontSize(8);
        doc.setTextColor(100);
        doc.text(
            `PureLedger - ${title || 'Report'} | Page ${i} of ${pageCount}`,
            14,
            doc.internal.pageSize.height - 10
        );
    }

    if (onProgress) onProgress(1);
    await delay(10);

    doc.save(filename);
};

