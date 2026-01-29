# PureLedger for Textile & Garment

**Order Tracking & Logistics for the Fashion Industry**

---

## Your Industry Challenges

| Challenge | Business Impact |
|-----------|-----------------|
| **Order Tracking** | Large orders ship in multiple lots, hard to track what's delivered |
| **Shipment Documentation** | Buyers need accurate delivery challans and invoices |
| **Multiple Ship-To Locations** | Same order may ship to different ports or warehouses |
| **Freight Complexity** | Different freight arrangements per customer/shipment |
| **Seasonal Business** | Need to track performance by season/collection |

---

## How PureLedger Solves These

### Base Voucher for Order Management
Create a base voucher for the full order, ship in partial lots.

```
Base Voucher: Order #PO-2024-001
├── Product: Style ABC (qty: 10,000)
├── Product: Style XYZ (qty: 5,000)
└── Status: Partial (6,000 shipped)

Derived Vouchers:
├── Shipment 1: 3,000 units → DC processed
├── Shipment 2: 3,000 units → DC processed
└── Remaining: 9,000 units pending
```

- Track large orders across multiple shipments
- See remaining quantities instantly
- Derived vouchers link back to original order
- Complete audit trail for buyers

### Comprehensive Shipment Documentation
Every delivery generates proper documentation.

| Document | Data Captured |
|----------|---------------|
| Delivery Challan | Products, quantities, locations |
| Reference | Builty number, carrier info |
| Station | Delivery destination |
| Remarks | Special instructions |

### Multi-Location Delivery
Handle different ship-to destinations on the same order.

- Define all delivery locations (ports, warehouses, stores)
- Each DC specifies exact delivery location
- Location-filtered reports show what went where
- Perfect for buyers with multiple receiving points

### Flexible Freight Handling
Different freight arrangements handled cleanly.

| Arrangement | PureLedger Approach |
|-------------|---------------------|
| FOB (Freight collect) | No FOR charges, just track shipment |
| CIF (Freight paid) | FOR charges posted to expense |
| Ex-Works | Cartage posted to transporter |
| Customer-paid freight | FOR posted to customer account |

### Season/Collection Tracking
Use projects or categories to organize by season.

```
Option 1: Project per Season
├── Project: Spring 2024
├── Project: Fall 2024
└── Project: Spring 2025

Option 2: Category per Collection
└── Project: Main Company
    ├── Category: Formal Collection
    ├── Category: Casual Collection
    └── Category: Denim Line
```

---

## Key Features for Textile & Garment

| Feature | Benefit |
|---------|---------|
| Base voucher system | Order management with partial shipments |
| Delivery challan processing | Professional shipment documentation |
| Location management | Multiple ship-to destinations |
| FOR/cartage automation | Freight cost tracking |
| Product reports | Style-level performance analysis |
| Multi-project | Season or collection organization |

---

## Typical Results

| Metric | Before PureLedger | After PureLedger |
|--------|-------------------|------------------|
| Order fulfillment visibility | Spreadsheet updates | Real-time |
| Shipment documentation time | 20-30 min | 5 min |
| Freight allocation accuracy | 75% | 98%+ |
| Buyer inquiry response time | Hours | Minutes |
| Season-end reconciliation | 1-2 weeks | Days |

---

## Ideal Customer Profile

- **Business Type:** Textile manufacturers, garment exporters, fashion distributors
- **Order Patterns:** Large orders with multiple shipments
- **Pain Points:** Order tracking, shipment documentation, buyer reporting
- **Team Size:** 5-50+ users
- **Markets:** Export-focused, multiple delivery destinations

---

## Textile/Garment Workflow Example

```
1. Receive Purchase Order
   └── Create base voucher with all line items

2. First Production Lot Ready
   └── Create derived voucher for shipped quantity
   └── Process DC with location, reference, carrier
   └── FOR charges if applicable

3. Second Production Lot Ready
   └── Create another derived voucher
   └── Process DC for this shipment
   └── Different location if needed

4. Buyer Inquiry
   └── View base voucher: see total vs. shipped vs. pending
   └── Print DCs for what's shipped
   └── Generate invoice for completed shipments

5. Season End
   └── Product report shows margin by style
   └── Base vouchers show outstanding orders
   └── Complete shipment history for each buyer
```

---

## Customer Quote

> "Our buyers order 50,000 pieces and we ship in 10 lots over 3 months. Tracking that used to be a nightmare. Base vouchers with derived vouchers give us perfect visibility into what's shipped and what's pending."
>
> — *Export Manager, Garment Manufacturer*

---

## Get Started

**See PureLedger in Action**
Schedule a demo customized for textile & garment operations.

**Email:** sales@pureledger.com
**Demo:** demo.pureledger.com

---

*PureLedger - Where Accounting Meets Logistics*
