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

const ProductReportPDFDocument = ({ 
    products,
    totals,
    formData,
    projects,
    categories,
    locations,
    totalRecords
}) => {
    const selectedProject = projects.find(p => p.id === formData.projectId);
    const selectedCategory = categories.find(c => c.id === formData.categoryId);
    const selectedLocation = locations.find(l => l.id === formData.locationId);

    // Fixed column widths to prevent cropping
    const COL_PRODUCT = 120;
    const COL_VOUCHER = 70;
    const COL_DATE = 70;
    const COL_QTY = 65;
    const COL_RATE = 70;
    const COL_AMOUNT = 75;
    const COL_COGS = 75;
    const COL_MARGIN = 75;
    const COL_MARGIN_PCT = 60;
    
    // Calculate total width and scale if needed
    const baseWidth = COL_PRODUCT + COL_VOUCHER + (COL_DATE * 2) + (COL_QTY * 3) + COL_RATE + (COL_AMOUNT * 2) + COL_COGS + COL_MARGIN + COL_MARGIN_PCT;
    const scale = PAGE_WIDTH / baseWidth;

    const COL_WIDTHS = {
        product: Math.floor(COL_PRODUCT * scale),
        voucher: Math.floor(COL_VOUCHER * scale),
        date: Math.floor(COL_DATE * scale),
        qty: Math.floor(COL_QTY * scale),
        rate: Math.floor(COL_RATE * scale),
        amount: Math.floor(COL_AMOUNT * scale),
        cogs: Math.floor(COL_COGS * scale),
        margin: Math.floor(COL_MARGIN * scale),
        marginPct: Math.floor(COL_MARGIN_PCT * scale),
    };

    // Split products into pages (15 rows per page)
    const chunkArray = (array, chunkSize) => {
        const chunks = [];
        for (let i = 0; i < array.length; i += chunkSize) {
            chunks.push(array.slice(i, i + chunkSize));
        }
        return chunks;
    };

    const productPages = chunkArray(products, ROWS_PER_PAGE);
    const totalPages = productPages.length;

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
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.product }]}>
                <Text style={styles.tableCellHeader}>Product</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.voucher }]}>
                <Text style={styles.tableCellHeader}>Voucher</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCellHeader}>Entry Date</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCellHeader}>Due Date</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellHeader}>Debit Qty</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellHeader}>Credit Qty</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellHeader}>Balance Qty</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.rate }]}>
                <Text style={styles.tableCellHeader}>Avg Rate</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellHeader}>Total Debit</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellHeader}>Total Credit</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.cogs }]}>
                <Text style={styles.tableCellHeader}>COGS</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.margin }]}>
                <Text style={styles.tableCellHeader}>Margin</Text>
            </View>
            <View style={[styles.tableColHeader, { width: COL_WIDTHS.marginPct }]}>
                <Text style={styles.tableCellHeader}>Margin %</Text>
            </View>
        </View>
    );

    const renderTableRow = (product, index) => (
        <View key={index} style={styles.tableRow}>
            <View style={[styles.tableCol, { width: COL_WIDTHS.product }]}>
                <Text style={styles.tableCell}>{product.productName || ''}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.voucher }]}>
                <Text style={styles.tableCell}>JV-{product.lastVoucherId || ''}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCell}>{product.entryDate || '-'}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCell}>{product.creditableDate || '-'}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellRight}>{product.totalDebitQty || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellRight}>{product.totalCreditQty || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellRight}>{product.qtyBalance || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.rate }]}>
                <Text style={styles.tableCellRight}>{formatCurrency(product.averageRate || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellRight}>{formatCurrency(product.totalDebit || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellRight}>{formatCurrency(product.totalCredit || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.cogs }]}>
                <Text style={styles.tableCellRight}>{formatCurrency(product.cogs || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.margin }]}>
                <Text style={styles.tableCellRight}>{formatCurrency(product.margin || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.marginPct }]}>
                <Text style={styles.tableCellRight}>{product.marginPercentage?.toFixed(2) || '0.00'}%</Text>
            </View>
        </View>
    );

    const renderTableFooter = () => (
        <View style={styles.tableRow}>
            <View style={[styles.tableCol, { width: COL_WIDTHS.product }]}>
                <Text style={styles.tableCellBold}>TOTAL</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.voucher }]}>
                <Text style={styles.tableCell}></Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCell}></Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.date }]}>
                <Text style={styles.tableCell}></Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellBoldRight}>{totals?.totalDebitQty || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellBoldRight}>{totals?.totalCreditQty || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.qty }]}>
                <Text style={styles.tableCellBoldRight}>{totals?.netQuantity || 0}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.rate }]}>
                <Text style={styles.tableCellBoldRight}>-</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totals?.totalDebit || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.amount }]}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totals?.totalCredit || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.cogs }]}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totals?.totalCOGS || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.margin }]}>
                <Text style={styles.tableCellBoldRight}>{formatCurrency(totals?.totalMargin || 0)}</Text>
            </View>
            <View style={[styles.tableCol, { width: COL_WIDTHS.marginPct }]}>
                <Text style={styles.tableCellBoldRight}>{totals?.marginPercentage?.toFixed(2) || '0.00'}%</Text>
            </View>
        </View>
    );

    return (
        <Document>
            {productPages.map((pageProducts, pageIndex) => (
                <Page key={pageIndex} size="A4" orientation="landscape" style={styles.page}>
                    <View style={styles.header} fixed>
                        <Text style={styles.title}>Product Stock & Margin Report</Text>
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
                        {selectedLocation && (
                            <Text style={styles.subtitle}>Location: {selectedLocation.name}</Text>
                        )}
                        <Text style={styles.subtitle}>
                            Generated: {new Date().toLocaleString('en-PK')} | Total Products: {totalRecords || products.length}
                        </Text>
                    </View>

                    <View style={styles.table}>
                        {renderTableHeader()}
                        {pageProducts.map((product, index) => renderTableRow(product, index))}
                        {pageIndex === totalPages - 1 && totals && renderTableFooter()}
                    </View>

                    <View style={styles.footer} fixed>
                        <Text render={({ pageNumber, totalPages }) => 
                            `PureLedger - Product Report | Page ${pageNumber} of ${totalPages}`
                        } />
                    </View>
                </Page>
            ))}
        </Document>
    );
};

export default ProductReportPDFDocument;

