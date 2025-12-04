import React from 'react';
import { Document, Page, Text, View, StyleSheet } from '@react-pdf/renderer';

// Landscape A4: 842 points wide, minus 60 padding = 782 available
const PAGE_WIDTH = 782;
const ROWS_PER_PAGE = 15;

const formatCurrency = (value) => {
    return new Intl.NumberFormat('en-PK', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 4
    }).format(value || 0);
};

const formatDate = (dateStr) => {
    if (!dateStr) return '';
    try {
        const date = new Date(dateStr);
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const year = date.getFullYear();
        return `${month}/${day}/${year}`;
    } catch {
        return dateStr;
    }
};

const MasterReportPDFDocument = ({ 
    entries, 
    formData, 
    projects, 
    categories, 
    accounts, 
    bookers,
    visibleColumns,
    totals,
    totalRecords
}) => {
    const selectedProject = projects.find(p => p.id === formData.projectId);
    const selectedCategory = categories.find(c => c.id === formData.categoryId);
    const selectedAccount = accounts.find(a => a.id === formData.accountId);
    const selectedBooker = bookers.find(b => b.id === formData.bookerId);

    const buildVoucherText = (entry) => {
        if (entry.formattedId === "OPENING" || entry.description === "Opening Balance") {
            return "OPENING";
        }
        return `JV-${entry.journalVoucherId}\nEntry: ${entry.id}`;
    };

    // Calculate column widths dynamically based on visible columns
    const getColumnWidths = () => {
        const visibleCols = [
            { key: 'id', width: 90, alwaysVisible: true },
            { key: 'date', width: 75, visible: visibleColumns.date },
            { key: 'creditableDate', width: 85, visible: visibleColumns.creditableDate },
            { key: 'description', width: 150, visible: visibleColumns.description },
            { key: 'account', width: 120, visible: visibleColumns.account },
            { key: 'booker', width: 90, visible: visibleColumns.booker },
            { key: 'quantity', width: 70, visible: visibleColumns.quantity },
            { key: 'debit', width: 85, visible: visibleColumns.debit },
            { key: 'credit', width: 85, visible: visibleColumns.credit },
            { key: 'balance', width: 85, visible: visibleColumns.balance },
        ];

        const visible = visibleCols.filter(col => col.alwaysVisible || col.visible);
        const totalWidth = visible.reduce((sum, col) => sum + col.width, 0);
        
        // Scale to fit page width
        const scale = PAGE_WIDTH / totalWidth;
        
        return visibleCols.map(col => ({
            key: col.key,
            width: (col.alwaysVisible || col.visible) ? Math.floor(col.width * scale) : 0
        }));
    };

    const columnWidths = getColumnWidths();
    const getWidth = (key) => {
        const col = columnWidths.find(c => c.key === key);
        return col ? col.width : 0;
    };

    // Split entries into pages (15 rows per page)
    const chunkArray = (array, chunkSize) => {
        const chunks = [];
        for (let i = 0; i < array.length; i += chunkSize) {
            chunks.push(array.slice(i, i + chunkSize));
        }
        return chunks;
    };

    const entryPages = chunkArray(entries, ROWS_PER_PAGE);
    const totalPages = entryPages.length;

    const styles = StyleSheet.create({
        page: {
            padding: 30,
            fontSize: 10,
            fontFamily: 'Helvetica',
        },
        header: {
            marginBottom: 15,
            borderBottom: '2 solid #2563eb',
            paddingBottom: 10,
        },
        title: {
            fontSize: 20,
            fontWeight: 'bold',
            color: '#1e40af',
            marginBottom: 5,
        },
        subtitle: {
            fontSize: 10,
            color: '#64748b',
            marginBottom: 2,
        },
        table: {
            display: 'table',
            width: PAGE_WIDTH,
            borderStyle: 'solid',
            borderWidth: 1,
            borderRightWidth: 0,
            borderBottomWidth: 0,
            marginTop: 10,
        },
        tableRow: {
            margin: 'auto',
            flexDirection: 'row',
        },
        tableColHeader: {
            borderStyle: 'solid',
            borderWidth: 1,
            borderLeftWidth: 0,
            borderTopWidth: 0,
            backgroundColor: '#f1f5f9',
            padding: 6,
        },
        tableCol: {
            borderStyle: 'solid',
            borderWidth: 1,
            borderLeftWidth: 0,
            borderTopWidth: 0,
            padding: 5,
        },
        tableCellHeader: {
            fontSize: 8,
            fontWeight: 'bold',
            color: '#1e293b',
        },
        tableCell: {
            fontSize: 7,
            color: '#334155',
        },
        tableCellRight: {
            fontSize: 7,
            color: '#334155',
            textAlign: 'right',
        },
        tableCellBold: {
            fontSize: 8,
            fontWeight: 'bold',
            color: '#1e293b',
        },
        tableCellBoldRight: {
            fontSize: 8,
            fontWeight: 'bold',
            color: '#1e293b',
            textAlign: 'right',
        },
        openingRow: {
            backgroundColor: '#dbeafe',
        },
        footer: {
            marginTop: 15,
            paddingTop: 10,
            borderTop: '1 solid #e2e8f0',
            fontSize: 8,
            color: '#64748b',
            textAlign: 'center',
        },
    });

    const renderTableHeader = () => (
        <View style={styles.tableRow}>
            <View style={[styles.tableColHeader, { width: getWidth('id') }]}>
                <Text style={styles.tableCellHeader}>ID</Text>
            </View>
            {visibleColumns.date && (
                <View style={[styles.tableColHeader, { width: getWidth('date') }]}>
                    <Text style={styles.tableCellHeader}>Entry Date</Text>
                </View>
            )}
            {visibleColumns.creditableDate && (
                <View style={[styles.tableColHeader, { width: getWidth('creditableDate') }]}>
                    <Text style={styles.tableCellHeader}>Creditable Date</Text>
                </View>
            )}
            {visibleColumns.description && (
                <View style={[styles.tableColHeader, { width: getWidth('description') }]}>
                    <Text style={styles.tableCellHeader}>Description</Text>
                </View>
            )}
            {visibleColumns.account && (
                <View style={[styles.tableColHeader, { width: getWidth('account') }]}>
                    <Text style={styles.tableCellHeader}>Account</Text>
                </View>
            )}
            {visibleColumns.booker && (
                <View style={[styles.tableColHeader, { width: getWidth('booker') }]}>
                    <Text style={styles.tableCellHeader}>Booker</Text>
                </View>
            )}
            {visibleColumns.quantity && (
                <View style={[styles.tableColHeader, { width: getWidth('quantity') }]}>
                    <Text style={styles.tableCellHeader}>Quantity</Text>
                </View>
            )}
            {visibleColumns.debit && (
                <View style={[styles.tableColHeader, { width: getWidth('debit') }]}>
                    <Text style={styles.tableCellHeader}>Debit</Text>
                </View>
            )}
            {visibleColumns.credit && (
                <View style={[styles.tableColHeader, { width: getWidth('credit') }]}>
                    <Text style={styles.tableCellHeader}>Credit</Text>
                </View>
            )}
            {visibleColumns.balance && (
                <View style={[styles.tableColHeader, { width: getWidth('balance') }]}>
                    <Text style={styles.tableCellHeader}>Balance</Text>
                </View>
            )}
        </View>
    );

    const renderTableRow = (entry, index) => {
        const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";
        return (
            <View key={index} style={[styles.tableRow, isOpening && styles.openingRow]}>
                <View style={[styles.tableCol, { width: getWidth('id') }]}>
                    <Text style={styles.tableCell}>{buildVoucherText(entry)}</Text>
                </View>
                {visibleColumns.date && (
                    <View style={[styles.tableCol, { width: getWidth('date') }]}>
                        <Text style={styles.tableCell}>{formatDate(entry.transactionDate)}</Text>
                    </View>
                )}
                {visibleColumns.creditableDate && (
                    <View style={[styles.tableCol, { width: getWidth('creditableDate') }]}>
                        <Text style={styles.tableCell}>{formatDate(entry.creditableDate)}</Text>
                    </View>
                )}
                {visibleColumns.description && (
                    <View style={[styles.tableCol, { width: getWidth('description') }]}>
                        <Text style={styles.tableCell}>{entry.description || ''}</Text>
                    </View>
                )}
                {visibleColumns.account && (
                    <View style={[styles.tableCol, { width: getWidth('account') }]}>
                        <Text style={styles.tableCell}>{entry.accountName || ''}</Text>
                    </View>
                )}
                {visibleColumns.booker && (
                    <View style={[styles.tableCol, { width: getWidth('booker') }]}>
                        <Text style={styles.tableCell}>{entry.bookerName || ''}</Text>
                    </View>
                )}
                {visibleColumns.quantity && (
                    <View style={[styles.tableCol, { width: getWidth('quantity') }]}>
                        <Text style={styles.tableCellRight}>{entry.quantity ?? ''}</Text>
                    </View>
                )}
                {visibleColumns.debit && (
                    <View style={[styles.tableCol, { width: getWidth('debit') }]}>
                        <Text style={styles.tableCellRight}>{formatCurrency(entry.debit)}</Text>
                    </View>
                )}
                {visibleColumns.credit && (
                    <View style={[styles.tableCol, { width: getWidth('credit') }]}>
                        <Text style={styles.tableCellRight}>{formatCurrency(entry.credit)}</Text>
                    </View>
                )}
                {visibleColumns.balance && (
                    <View style={[styles.tableCol, { width: getWidth('balance') }]}>
                        <Text style={styles.tableCellRight}>{formatCurrency(entry.balance)}</Text>
                    </View>
                )}
            </View>
        );
    };

    const renderTableFooter = () => (
        <View style={styles.tableRow}>
            <View style={[styles.tableCol, { width: getWidth('id') }]}>
                <Text style={styles.tableCellBold}>TOTAL</Text>
            </View>
            {visibleColumns.date && <View style={[styles.tableCol, { width: getWidth('date') }]}><Text style={styles.tableCell}></Text></View>}
            {visibleColumns.creditableDate && <View style={[styles.tableCol, { width: getWidth('creditableDate') }]}><Text style={styles.tableCell}></Text></View>}
            {visibleColumns.description && <View style={[styles.tableCol, { width: getWidth('description') }]}><Text style={styles.tableCell}></Text></View>}
            {visibleColumns.account && <View style={[styles.tableCol, { width: getWidth('account') }]}><Text style={styles.tableCell}></Text></View>}
            {visibleColumns.booker && <View style={[styles.tableCol, { width: getWidth('booker') }]}><Text style={styles.tableCell}></Text></View>}
            {visibleColumns.quantity && (
                <View style={[styles.tableCol, { width: getWidth('quantity') }]}>
                    <Text style={styles.tableCellBoldRight}>{totals.quantity}</Text>
                </View>
            )}
            {visibleColumns.debit && (
                <View style={[styles.tableCol, { width: getWidth('debit') }]}>
                    <Text style={styles.tableCellBoldRight}>{formatCurrency(totals.debit)}</Text>
                </View>
            )}
            {visibleColumns.credit && (
                <View style={[styles.tableCol, { width: getWidth('credit') }]}>
                    <Text style={styles.tableCellBoldRight}>{formatCurrency(totals.credit)}</Text>
                </View>
            )}
            {visibleColumns.balance && (
                <View style={[styles.tableCol, { width: getWidth('balance') }]}>
                    <Text style={styles.tableCellBoldRight}>{formatCurrency(totals.debit - totals.credit)}</Text>
                </View>
            )}
        </View>
    );

    return (
        <Document>
            {entryPages.map((pageEntries, pageIndex) => (
                <Page key={pageIndex} size="A4" orientation="landscape" style={styles.page}>
                    <View style={styles.header} fixed>
                        <Text style={styles.title}>Master Report</Text>
                        <Text style={styles.subtitle}>
                            Project: {selectedProject?.name || 'All Projects'}
                        </Text>
                        {formData.startDate && (
                            <Text style={styles.subtitle}>
                                Date Range: {formatDate(formData.startDate)} to {formatDate(formData.endDate || formData.startDate)}
                            </Text>
                        )}
                        {selectedCategory && (
                            <Text style={styles.subtitle}>Category: {selectedCategory.name}</Text>
                        )}
                        {selectedAccount && (
                            <Text style={styles.subtitle}>Account: {selectedAccount.name}</Text>
                        )}
                        {selectedBooker && (
                            <Text style={styles.subtitle}>Booker: {selectedBooker.name}</Text>
                        )}
                        <Text style={styles.subtitle}>
                            Generated: {new Date().toLocaleString('en-PK')} | Total Records: {totalRecords || entries.length}
                        </Text>
                    </View>

                    <View style={styles.table}>
                        {renderTableHeader()}
                        {pageEntries.map((entry, index) => renderTableRow(entry, index))}
                        {pageIndex === totalPages - 1 && renderTableFooter()}
                    </View>

                    <View style={styles.footer} fixed>
                        <Text render={({ pageNumber, totalPages }) => 
                            `PureLedger - Master Report | Page ${pageNumber} of ${totalPages}`
                        } />
                    </View>
                </Page>
            ))}
        </Document>
    );
};

export default MasterReportPDFDocument;
