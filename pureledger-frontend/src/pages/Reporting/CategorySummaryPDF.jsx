import React from 'react';
import { Document, Page, Text, View, StyleSheet } from '@react-pdf/renderer';

// Portrait A4: 595 points wide, minus 80 padding = 515 available
const PAGE_WIDTH = 515;
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

const CategorySummaryPDFDocument = ({ 
    categorySummary,
    formData,
    projects
}) => {
    const selectedProject = projects.find(p => p.id === formData.projectId);

    // Fixed column widths to prevent cropping
    const COL_ID = 100;
    const COL_NAME = 200;
    const COL_AMOUNT = 70;
    const TOTAL_WIDTH = COL_ID + COL_NAME + (COL_AMOUNT * 3);

    // Split entries into pages (15 rows per page)
    const chunkArray = (array, chunkSize) => {
        const chunks = [];
        for (let i = 0; i < array.length; i += chunkSize) {
            chunks.push(array.slice(i, i + chunkSize));
        }
        return chunks;
    };

    const entryPages = chunkArray(categorySummary, ROWS_PER_PAGE);
    const totalPages = entryPages.length;

    const totalDebit = categorySummary.reduce((sum, item) => sum + (item.debit || 0), 0);
    const totalCredit = categorySummary.reduce((sum, item) => sum + (item.credit || 0), 0);
    const totalBalance = totalDebit - totalCredit;

    const styles = StyleSheet.create({
        page: {
            padding: 40,
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
            width: TOTAL_WIDTH,
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
        tableColId: {
            width: COL_ID,
            borderStyle: 'solid',
            borderWidth: 1,
            borderLeftWidth: 0,
            borderTopWidth: 0,
            padding: 6,
        },
        tableColName: {
            width: COL_NAME,
            borderStyle: 'solid',
            borderWidth: 1,
            borderLeftWidth: 0,
            borderTopWidth: 0,
            padding: 6,
        },
        tableColAmount: {
            width: COL_AMOUNT,
            borderStyle: 'solid',
            borderWidth: 1,
            borderLeftWidth: 0,
            borderTopWidth: 0,
            padding: 6,
        },
        tableColHeader: {
            backgroundColor: '#f1f5f9',
        },
        tableCellHeader: {
            fontSize: 9,
            fontWeight: 'bold',
            color: '#1e293b',
        },
        tableCell: {
            fontSize: 8,
            color: '#334155',
        },
        tableCellRight: {
            fontSize: 8,
            color: '#334155',
            textAlign: 'right',
        },
        tableCellBold: {
            fontSize: 9,
            fontWeight: 'bold',
            color: '#1e293b',
        },
        tableCellBoldRight: {
            fontSize: 9,
            fontWeight: 'bold',
            color: '#1e293b',
            textAlign: 'right',
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
            <View style={[styles.tableColId, styles.tableColHeader]}>
                <Text style={styles.tableCellHeader}>ID</Text>
            </View>
            <View style={[styles.tableColName, styles.tableColHeader]}>
                <Text style={styles.tableCellHeader}>Category</Text>
            </View>
            <View style={[styles.tableColAmount, styles.tableColHeader]}>
                <Text style={styles.tableCellHeader}>Debit</Text>
            </View>
            <View style={[styles.tableColAmount, styles.tableColHeader]}>
                <Text style={styles.tableCellHeader}>Credit</Text>
            </View>
            <View style={[styles.tableColAmount, styles.tableColHeader]}>
                <Text style={styles.tableCellHeader}>Balance</Text>
            </View>
        </View>
    );

    const renderTableRow = (item, index) => {
        const balance = item.debit - item.credit;
        return (
            <View key={index} style={styles.tableRow}>
                <View style={styles.tableColId}>
                    <Text style={styles.tableCell}>{item.id}</Text>
                </View>
                <View style={styles.tableColName}>
                    <Text style={styles.tableCell}>{item.name}</Text>
                </View>
                <View style={styles.tableColAmount}>
                    <Text style={styles.tableCellRight}>{formatCurrency(item.debit)}</Text>
                </View>
                <View style={styles.tableColAmount}>
                    <Text style={styles.tableCellRight}>{formatCurrency(item.credit)}</Text>
                </View>
                <View style={styles.tableColAmount}>
                    <Text style={styles.tableCellRight}>{formatCurrency(balance)}</Text>
                </View>
            </View>
        );
    };

    const renderTableFooter = () => (
        <View style={styles.tableRow}>
            <View style={styles.tableColId}>
                <Text style={styles.tableCellBold}>TOTAL</Text>
            </View>
            <View style={styles.tableColName}>
                <Text style={styles.tableCellBold}></Text>
            </View>
            <View style={styles.tableColAmount}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totalDebit)}</Text>
            </View>
            <View style={styles.tableColAmount}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totalCredit)}</Text>
            </View>
            <View style={styles.tableColAmount}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totalBalance)}</Text>
            </View>
        </View>
    );

    return (
        <Document>
            {entryPages.map((pageEntries, pageIndex) => (
                <Page key={pageIndex} size="A4" orientation="portrait" style={styles.page}>
                    <View style={styles.header} fixed>
                        <Text style={styles.title}>Category Summary</Text>
                        <Text style={styles.subtitle}>
                            Project: {selectedProject?.name || 'All Projects'}
                        </Text>
                        {formData.startDate && (
                            <Text style={styles.subtitle}>
                                Date Range: {formatDate(formData.startDate)} to {formatDate(formData.endDate || formData.startDate)}
                            </Text>
                        )}
                        <Text style={styles.subtitle}>
                            Generated: {new Date().toLocaleString('en-PK')} | Total Categories: {categorySummary.length}
                        </Text>
                    </View>

                    <View style={styles.table}>
                        {renderTableHeader()}
                        {pageEntries.map((item, index) => renderTableRow(item, index))}
                        {pageIndex === totalPages - 1 && renderTableFooter()}
                    </View>

                    <View style={styles.footer} fixed>
                        <Text render={({ pageNumber, totalPages }) => 
                            `PureLedger - Category Summary | Page ${pageNumber} of ${totalPages}`
                        } />
                    </View>
                </Page>
            ))}
        </Document>
    );
};

export default CategorySummaryPDFDocument;
