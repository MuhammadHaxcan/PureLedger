# PureLedger Product Brochure

**Enterprise Accounting & Logistics Intelligence for Trading Businesses**

---

## Page 1: Cover

# PureLedger

### Where Accounting Meets Logistics

*Enterprise-grade accounting precision combined with integrated delivery tracking for trading and distribution businesses*

**www.pureledger.com**

---

## Page 2: The Trading Business Challenge

### Your Books Don't Match Your Operations

Trading companies face unique challenges that generic accounting software simply wasn't designed to handle:

| Challenge | Business Impact |
|-----------|-----------------|
| **Disconnected Systems** | Delivery data lives in spreadsheets while accounting lives in software - reconciliation becomes a monthly nightmare |
| **Freight Allocation Chaos** | FOR charges and cartage costs get lost or misallocated, eating into margins |
| **Credit Visibility Gap** | By the time you know a customer is over credit limit, the goods are already delivered |
| **Multi-Location Complexity** | Tracking inventory and deliveries across locations requires constant manual effort |
| **Reporting Delays** | Month-end close takes weeks because data has to be pulled from multiple sources |

### The Hidden Cost of Disconnection

> **60%** of trading companies report spending 10+ hours per month reconciling delivery challans with invoices.

> **$12,000** average annual cost of freight misallocation for mid-sized distributors.

> **3 weeks** typical delay in identifying customer credit issues.

**There's a better way.**

---

## Page 3: Introducing PureLedger

### One Platform. Complete Control.

PureLedger is the first accounting platform built specifically for trading and distribution businesses, integrating rigorous double-entry bookkeeping with native logistics tracking.

#### Core Philosophy

**Precision Accounting**
Every transaction recorded with double-entry integrity

**Integrated Logistics**
Delivery challans and invoices created together, not separately

**Real-Time Visibility**
Customer balances, inventory positions, and delivery status - always current

**Role-Based Access**
Everyone sees what they need, nothing more

---

### What Makes PureLedger Different

| Traditional Software | PureLedger |
|---------------------|------------|
| Accounting and logistics are separate systems | Unified platform - DC and invoice created together |
| Manual freight allocation | Automatic FOR and cartage posting |
| Monthly reconciliation required | Always reconciled by design |
| Generic reporting | Trading-specific reports (Aging, Master, Product) |
| One-size-fits-all access | Granular role-based permissions |
| Single company only | Multi-project architecture for business units |

---

## Page 4: Core Accounting Features

### Double-Entry Bookkeeping Done Right

PureLedger implements rigorous double-entry bookkeeping with trading-specific enhancements.

#### Journal Voucher System

**Complete Transaction Recording**
- Multi-line vouchers with unlimited entries
- Automatic debit/credit balancing
- Real-time validation prevents unbalanced entries
- Support for complex transactions with multiple accounts

**Entry Types Supported**
- Account entries (customers, suppliers, banks)
- Product entries (inventory with quantity tracking)
- Mixed transactions in a single voucher

**Reference Tracking**
- Linked vouchers for related transactions
- Complete audit trail from original entry to final posting
- Easy navigation between related documents

#### Multi-Project Architecture

Separate books for separate business units, with unified reporting when you need it.

| Feature | Benefit |
|---------|---------|
| Independent project ledgers | Each business unit maintains separate accounts |
| Cross-project reporting | Consolidated views when needed |
| Project-specific settings | Customize FOR accounts, cartage settings per project |
| User-project permissions | Control who accesses which projects |

---

## Page 5: Delivery Challan Integration

### Native DC Processing - Not an Afterthought

Unlike other systems where delivery tracking is bolted on, PureLedger builds DC processing directly into the accounting workflow.

#### How It Works

```
1. Create Sale Voucher
   └── Product entries with quantities

2. Process Delivery Challan
   └── Select entries for this delivery
   └── Assign location, station, reference
   └── System marks entries as processed

3. Automatic Tracking
   └── Voucher completion status updates
   └── Partial deliveries supported
   └── Complete audit trail maintained
```

#### DC Features

**Location Management**
- Define delivery locations (Addas, stations, branches)
- Track which products went where
- Location-based reporting

**Reference Tracking**
- Builty numbers for transport tracking
- Station references
- Custom remarks per delivery

**Status Intelligence**
- See which vouchers have pending deliveries
- Identify unprocessed product entries
- Track partial vs. complete deliveries

---

## Page 6: FOR & Cartage Automation

### Freight Charges, Automatically Allocated

FOR (Freight-On-Road) charges and cartage costs are automatically posted and optionally allocated to customer accounts.

#### FOR Charges Flow

```
Sale Voucher Created
        ↓
Delivery Challan Processed
        ↓
FOR Charges Option Selected
        ↓
Automatic Journal Entry Created:
   - Debit: FOR Expense Account
   - Credit: Customer Account (if PostToCustomer)
        ↓
Reference Link Maintained
```

#### Cartage Processing

When goods are transported via third-party carriers:

| Step | Automatic Action |
|------|------------------|
| Enter cartage amount | System creates cartage voucher |
| Select transporter | Debit to cartage expense |
| Set PostToCustomer | Credit to customer account |
| Include FOR charges | Creates linked FOR voucher |

**Result:** Complete freight cost tracking with full audit trail back to original sale.

---

## Page 7: Comprehensive Reporting Suite

### Every Report a Trading Business Needs

PureLedger includes purpose-built reports for trading operations.

#### Aging Report
Track receivables by age with credit period awareness.
- Filter by project, category, account
- Creditable date vs. transaction date
- Identify overdue accounts instantly

#### Master Report
Complete transaction ledger with running balances.
- Date range filtering
- Project/category/account drill-down
- Booker-wise analysis
- Location filtering
- Running balance per entry
- Export to Excel

#### Account Report
Detailed account-level transaction history.
- All transactions for any account
- Running balance calculation
- Perfect for customer statements

#### Product Report
Inventory and margin analysis by product.
- Quantity in/out tracking
- Average cost calculation
- COGS and margin analysis
- Opening balance handling
- Location-wise breakdown

#### User Report
Permission-aware reporting for limited access users.
- Respects category-level permissions
- Users see only their authorized data
- Ideal for sales team access

#### Dashboard
Real-time business intelligence.
- Voucher counts and trends
- Completion status overview
- Recent activity monitoring
- Project-level metrics

---

## Page 8: Base Vouchers & Recipes

### Template Transactions for Repetitive Entries

For transactions that follow a pattern, Base Vouchers and Recipes eliminate repetitive data entry.

#### Base Voucher System

**What It Is:**
A template voucher that serves as the source for multiple derived transactions.

**How It Works:**
1. Create a Base Voucher with expected entries
2. Process partial quantities as actual sales occur
3. Derived vouchers created with actual quantities
4. Base voucher tracks remaining quantities
5. Full processing converts base to regular voucher

**Use Cases:**
- Standing orders with multiple deliveries
- Framework agreements with periodic releases
- Consignment arrangements

#### Recipe System

**What It Is:**
Saved transaction templates for instant voucher creation.

**Benefits:**
- One-click creation of complex vouchers
- Consistent entry patterns
- Reduced data entry errors
- Time savings on repetitive transactions

---

## Page 9: Security & Access Control

### Enterprise-Grade Security for Your Financial Data

PureLedger implements comprehensive security at every level.

#### Authentication

| Feature | Implementation |
|---------|----------------|
| Password Security | BCrypt adaptive hashing |
| Session Management | JWT tokens with configurable expiry |
| Token Validation | Issuer, audience, and signature verification |
| Session Timeout | Automatic logout after inactivity |

#### Role-Based Access Control (RBAC)

**Admin Users**
- Full system access
- User management
- All projects and categories

**Regular Users**
- Project-level permissions
- Category-level visibility control
- Action-level restrictions

**Permission Granularity:**
```
Controller Level
    └── Entry
    └── VoucherEntry
    └── BaseVouchers
    └── DeliveryChallans
    └── Reports
        └── SingleDoc
        └── AgingReport
        └── AccountReport
        └── MasterReport
        └── ProductReport
        └── UserReport
```

#### Data Protection

- All communication over HTTPS
- Database credentials secured
- Parameterized queries prevent SQL injection
- Input validation on all endpoints
- Error messages don't expose system details

---

## Page 10: Technical Architecture

### Built for Reliability and Performance

PureLedger is built on modern, proven technologies.

#### Technology Stack

**Backend**
- ASP.NET Core 8.0
- PostgreSQL database
- Entity Framework Core ORM
- RESTful API architecture

**Frontend**
- React 18 with modern hooks
- Responsive design for all devices
- Real-time updates
- Optimized for slow connections

#### Performance Features

| Feature | Benefit |
|---------|---------|
| Efficient queries | Batch loading prevents N+1 issues |
| Pagination | Handle large datasets smoothly |
| Caching | Reduce database load |
| Async processing | Non-blocking operations |

#### Deployment Options

**Cloud Hosted (Recommended)**
- Fully managed infrastructure
- Automatic backups
- 99.9% uptime SLA
- Automatic updates

**Self-Hosted**
- Deploy on your infrastructure
- Full data sovereignty
- Custom security policies
- Integration flexibility

---

## Page 11: Implementation & Support

### Get Running Quickly, Stay Supported

#### Implementation Process

**Week 1-2: Discovery & Setup**
- Business requirements review
- System configuration
- User account creation
- Data migration planning

**Week 3-4: Data Migration**
- Chart of accounts setup
- Opening balances import
- Historical data migration (optional)
- Validation and reconciliation

**Week 5-6: Training & Go-Live**
- User training sessions
- Parallel running (optional)
- Go-live support
- Issue resolution

#### Training Programs

| Program | Duration | Audience |
|---------|----------|----------|
| End User | 4 hours | Data entry staff |
| Power User | 8 hours | Accountants, supervisors |
| Admin | 4 hours | System administrators |
| Custom | Varies | Role-specific training |

#### Support Options

**Standard Support**
- Email support (24-hour response)
- Knowledge base access
- Monthly webinars

**Premium Support**
- Priority email (4-hour response)
- Phone support during business hours
- Dedicated success manager
- Quarterly business reviews

**Enterprise Support**
- 24/7 phone support
- Dedicated account team
- Custom development SLA
- On-site support visits

---

## Page 12: Get Started

### Take the Next Step

#### Request a Demo
See PureLedger in action with your actual business scenarios.
- 30-minute personalized demonstration
- Q&A with product specialist
- Custom workflow discussion

**Schedule at:** demo.pureledger.com

#### Start Free Trial
Experience PureLedger with your own data.
- 14-day full-feature access
- Sample data included
- Email support during trial
- No credit card required

**Start at:** trial.pureledger.com

#### Contact Sales
Discuss enterprise requirements and custom pricing.
- Volume discounts available
- Multi-year agreements
- Custom implementation
- Partner programs

**Email:** sales@pureledger.com
**Phone:** +1 (555) LEDGER-1

---

### Pricing Overview

| Edition | Users | Starting At |
|---------|-------|-------------|
| Starter | Up to 3 | $49/month |
| Professional | Up to 10 | $149/month |
| Business | Up to 25 | $349/month |
| Enterprise | Unlimited | Contact Sales |

*Regional pricing available for Pakistan, India, UAE, and other markets*

---

## Back Cover

### PureLedger

**Where Accounting Meets Logistics**

www.pureledger.com
sales@pureledger.com
+1 (555) LEDGER-1

**Deployed at:** tradeledger.cloud

---

*PureLedger - Enterprise Accounting for Trading Excellence*

**Connect With Us:**
- LinkedIn: /company/pureledger
- Twitter: @pureledger
- YouTube: /pureledger

---

**Document Version:** 1.0
**Last Updated:** January 2026
