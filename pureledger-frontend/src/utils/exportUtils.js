import { jsPDF } from 'jspdf';
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
        logoDataUrl,
        dateRange,
        generatedInfo,
        columns,
        rows,
        startY = 46,
        fontSize = 9,
        cellPadding = 2,
        filename
    } = config;

    const doc = new jsPDF(orientation, 'mm', format);
    let headerBottom = 20;

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
        headerBottom = yPos;
    }

    if (logoDataUrl) {
        const properties = doc.getImageProperties(logoDataUrl);
        const maxWidth = 34;
        const maxHeight = 22;
        const scale = Math.min(maxWidth / properties.width, maxHeight / properties.height);
        const width = properties.width * scale;
        const height = properties.height * scale;
        doc.addImage(logoDataUrl, properties.fileType, doc.internal.pageSize.width - 14 - width, 10, width, height);
        headerBottom = Math.max(headerBottom, 10 + height + 4);
    }

    if (onProgress) onProgress(0.3);
    await delay(10);

    autoTable(doc, {
        columns,
        body: rows,
        startY: Math.max(startY, headerBottom + 2),
        margin: { top: 20 },
        styles: { fontSize, cellPadding },
        headStyles: { fillColor: [241, 245, 249], textColor: [30, 41, 59], fontStyle: 'bold' },
        footStyles: { fillColor: [241, 245, 249], fontStyle: 'bold' }
    });

    if (onProgress) onProgress(0.8);
    await delay(10);

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

