# PureLedger Implementation Guide

**A Complete Guide to Deploying and Configuring PureLedger**

**Version 1.0 | January 2026**

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Pre-Implementation Planning](#2-pre-implementation-planning)
3. [System Requirements](#3-system-requirements)
4. [Initial Setup](#4-initial-setup)
5. [Chart of Accounts Configuration](#5-chart-of-accounts-configuration)
6. [User Setup & Permissions](#6-user-setup--permissions)
7. [Project Configuration](#7-project-configuration)
8. [Data Migration](#8-data-migration)
9. [Testing & Validation](#9-testing--validation)
10. [Go-Live Checklist](#10-go-live-checklist)
11. [Training Program](#11-training-program)
12. [Post-Implementation Support](#12-post-implementation-support)
13. [Appendices](#13-appendices)

---

## 1. Introduction

### About This Guide

This implementation guide provides step-by-step instructions for deploying PureLedger in your organization. Whether you're migrating from another system or starting fresh, this guide covers everything from initial planning to go-live and beyond.

### Implementation Timeline Overview

| Phase | Duration | Activities |
|-------|----------|------------|
| Planning | 1 week | Requirements gathering, team assignment |
| Setup | 1 week | System access, initial configuration |
| Configuration | 1-2 weeks | Chart of accounts, users, projects |
| Migration | 1-2 weeks | Data import, validation |
| Testing | 1 week | User acceptance testing |
| Training | 1 week | End user and admin training |
| Go-Live | 1 day | Cutover, monitoring |
| Stabilization | 2 weeks | Issue resolution, optimization |

**Typical Total Duration:** 6-8 weeks

### Key Stakeholders

| Role | Responsibilities |
|------|------------------|
| Project Sponsor | Decision authority, resource allocation |
| Project Manager | Timeline, coordination, issue escalation |
| Finance Lead | Chart of accounts, opening balances |
| Operations Lead | DC processes, locations, workflows |
| IT Lead | Technical setup, integrations |
| End Users | Testing, feedback, adoption |
| PureLedger Team | Configuration, support, training |

---

## 2. Pre-Implementation Planning

### 2.1 Requirements Gathering

Complete the following questionnaire before starting implementation:

#### Business Information

| Question | Your Answer |
|----------|-------------|
| Company name | |
| Industry | |
| Number of employees | |
| Number of PureLedger users | |
| Current accounting system | |
| Current DC tracking method | |

#### Volume Estimates

| Metric | Estimate |
|--------|----------|
| Monthly transactions | |
| Number of customers | |
| Number of suppliers | |
| Number of products | |
| Number of locations | |
| Number of projects/business units | |

#### Workflow Questions

| Question | Your Answer |
|----------|-------------|
| Do you process delivery challans? | Yes / No |
| Do you have FOR charges? | Yes / No |
| Do you have cartage charges? | Yes / No |
| Do you need multi-project accounting? | Yes / No |
| Do you use base vouchers/standing orders? | Yes / No |
| Which reports do you need? | |

### 2.2 Data Preparation Checklist

Before implementation, prepare the following data:

- [ ] Chart of accounts (account list)
- [ ] Customer master list
- [ ] Supplier master list
- [ ] Product master list
- [ ] Location master list
- [ ] User list with roles
- [ ] Opening balances as of cutover date
- [ ] Historical transactions (if migrating)

### 2.3 Success Criteria

Define what success looks like:

| Criterion | Target |
|-----------|--------|
| Data accuracy | 100% balances match source system |
| User adoption | All users trained and using system |
| Process efficiency | X% reduction in reconciliation time |
| Go-live date | [Date] |

---

## 3. System Requirements

### 3.1 Cloud Hosted (Recommended)

**Requirements:**
- Modern web browser (Chrome, Firefox, Edge, Safari)
- Stable internet connection
- No local installation required

**Supported Browsers:**

| Browser | Minimum Version |
|---------|-----------------|
| Chrome | 90+ |
| Firefox | 88+ |
| Edge | 90+ |
| Safari | 14+ |

### 3.2 Self-Hosted

**Server Requirements:**

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Storage | 20 GB | 100 GB SSD |
| OS | Windows Server 2019 / Ubuntu 20.04 | Latest stable |

**Software Requirements:**
- .NET 8.0 Runtime
- PostgreSQL 14+
- Nginx or IIS (reverse proxy)
- SSL certificate

**Network Requirements:**
- HTTPS (port 443)
- Database port (5432, internal only)

---

## 4. Initial Setup

### 4.1 Cloud Hosted Setup

1. **Account Creation**
   - Register at signup.pureledger.com
   - Verify email address
   - Complete company profile

2. **Initial Admin User**
   - Create admin username and password
   - Enable admin role
   - Record credentials securely

3. **Subscription Activation**
   - Select edition (Starter, Professional, Business)
   - Enter payment information
   - Confirm subscription

### 4.2 Self-Hosted Setup

1. **Database Setup**
   ```sql
   CREATE DATABASE pureledger;
   CREATE USER pureledger_app WITH ENCRYPTED PASSWORD 'your-secure-password';
   GRANT ALL PRIVILEGES ON DATABASE pureledger TO pureledger_app;
   ```

2. **Application Configuration**
   Update `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "LedgerDb": "Host=localhost;Port=5432;Database=pureledger;Username=pureledger_app;Password=your-secure-password"
     },
     "Jwt": {
       "Issuer": "PureLedger",
       "Audience": "PureLedgerUsers",
       "SecretKey": "your-32-character-secret-key",
       "ExpiryMinutes": "1440"
     },
     "Cors": {
       "AllowedOrigins": ["https://your-domain.com"]
     }
   }
   ```

3. **Database Migration**
   ```bash
   dotnet ef database update
   ```

4. **Application Deployment**
   - Deploy backend to server
   - Deploy frontend to web server
   - Configure reverse proxy

5. **SSL Configuration**
   - Install SSL certificate
   - Configure HTTPS redirect

---

## 5. Chart of Accounts Configuration

### 5.1 Account Structure

PureLedger uses a hierarchical account structure:

```
Project (Business Unit)
└── Category (Account Group)
    └── Account (Individual Account)
        └── Account Data (Extended Properties)
```

### 5.2 Account Groups

Two primary account groups:

| Group ID | Group Name | Purpose |
|----------|------------|---------|
| 1 | Account | Financial accounts (customers, suppliers, banks) |
| 2 | Product | Inventory items with quantity tracking |

### 5.3 Setting Up Categories

Categories organize accounts within a project.

**Recommended Category Structure:**

| Category | Account Group | Purpose |
|----------|---------------|---------|
| Customers | Account | Customer receivables |
| Suppliers | Account | Supplier payables |
| Banks | Account | Bank accounts |
| Cash | Account | Cash accounts |
| Expenses | Account | Expense accounts |
| Income | Account | Income accounts |
| Products | Product | Inventory items |
| Raw Materials | Product | Manufacturing inputs |

**Steps to Create Category:**
1. Navigate to MetaData > Categories
2. Select Project
3. Click "Add Category"
4. Enter Category Name
5. Select Account Group
6. Save

### 5.4 Setting Up Accounts

**Account Properties:**

| Property | Description | Required |
|----------|-------------|----------|
| Account Name | Display name | Yes |
| Category | Parent category | Yes |
| Account Group | Account or Product | Yes |
| Status | Active/Inactive | Yes |

**For Account-type Accounts (Group 1):**

| Property | Description |
|----------|-------------|
| Phone Number | Contact phone |
| Address | Physical address |
| City | City name |

**For Product-type Accounts (Group 2):**

| Property | Description |
|----------|-------------|
| PKG | Package size (for display) |

**Steps to Create Account:**
1. Navigate to MetaData > Accounts
2. Select Category
3. Click "Add Account"
4. Enter Account details
5. Save

### 5.5 Import Accounts from Excel

**Template Format:**

| Column | Description | Required |
|--------|-------------|----------|
| A | Account Name | Yes |
| B | Category Name | Yes |
| C | Account Group (Account/Product) | Yes |
| D | Phone Number | No |
| E | Address | No |
| F | City | No |

**Import Steps:**
1. Prepare data in template format
2. Navigate to Admin > Import
3. Upload Excel file
4. Review mappings
5. Confirm import

---

## 6. User Setup & Permissions

### 6.1 User Roles

| Role | Capabilities |
|------|--------------|
| Admin | Full system access, user management |
| User | Access based on permissions |

### 6.2 Permission Types

**Project Permissions:**
- Users can be assigned to specific projects
- Users only see data from assigned projects

**Category Permissions (Hidden Categories):**
- Categories can be hidden from users
- Hidden categories don't appear in reports

**Action Permissions:**
- Entry: Create journal entries
- VoucherEntry: View/edit voucher entries
- BaseVouchers: Work with base vouchers
- DeliveryChallans: Process DCs
- Reports (various): Run specific reports

### 6.3 Creating Users

**Steps:**
1. Navigate to Admin > Users
2. Click "Add User"
3. Enter:
   - Username
   - Password
   - Is Admin (checkbox)
4. Save user

**Assigning Permissions:**
1. Select user
2. Navigate to Permissions tab
3. Assign projects
4. Set hidden categories
5. Configure action permissions
6. Save

### 6.4 User Setup Best Practices

| Recommendation | Reason |
|----------------|--------|
| Use individual accounts | Audit trail accountability |
| Minimum necessary permissions | Security principle |
| Regular permission reviews | Access hygiene |
| Document permission decisions | Change management |

---

## 7. Project Configuration

### 7.1 Creating Projects

**Project Properties:**

| Property | Description |
|----------|-------------|
| Project Name | Display name |
| Project Image | Logo for documents |

**Steps:**
1. Navigate to MetaData > Projects
2. Click "Add Project"
3. Enter Project Name
4. Upload Project Image (optional)
5. Save

### 7.2 Project Cartage Settings

Configure FOR and cartage accounts per project.

**Settings:**

| Setting | Description |
|---------|-------------|
| FOR Expense Account | Account to debit FOR charges |
| Cartage Expense Category | Category for cartage expenses |
| Cartage Expense Account | Account to debit cartage |
| Transporter Category | Category for transporters |

**Steps:**
1. Navigate to Admin > Project Settings
2. Select Project
3. Configure cartage accounts
4. Save

### 7.3 Location Setup

Locations represent delivery destinations.

**Steps:**
1. Navigate to MetaData > Locations
2. Click "Add Location"
3. Enter Location Name
4. Save

---

## 8. Data Migration

### 8.1 Migration Approach Options

| Approach | Description | When to Use |
|----------|-------------|-------------|
| Opening Balances Only | Import balances as of cutover date | Clean start preferred |
| Full Historical | Import all historical transactions | Audit requirements |
| Hybrid | Balances + recent history | Balance of both needs |

### 8.2 Opening Balance Migration

**Steps:**

1. **Determine Cutover Date**
   - End of month recommended
   - All transactions complete in old system

2. **Extract Balances from Old System**
   - Trial balance as of cutover date
   - Customer balances
   - Supplier balances
   - Product quantities and values

3. **Prepare Opening Balance Voucher**
   - One voucher per project
   - Debit all asset accounts
   - Credit all liability accounts
   - Difference to equity

4. **Enter in PureLedger**
   - Create Journal Voucher dated as of cutover
   - Enter all opening balances
   - Verify total debits = total credits

5. **Validate**
   - Run trial balance in both systems
   - Compare and reconcile differences

### 8.3 Historical Data Migration

**For Full Historical Migration:**

1. **Export from Old System**
   - Transaction-level detail
   - Include all required fields

2. **Transform Data**
   - Map accounts to PureLedger format
   - Standardize dates
   - Handle currency formatting

3. **Import Process**
   - Use import API or Excel import
   - Process in batches
   - Validate each batch

4. **Reconciliation**
   - Compare period totals
   - Verify running balances
   - Check individual accounts

### 8.4 Migration Validation Checklist

- [ ] Total debits = Total credits
- [ ] Customer balances match source
- [ ] Supplier balances match source
- [ ] Bank balances match statements
- [ ] Product quantities match physical count
- [ ] Trial balance ties to source

---

## 9. Testing & Validation

### 9.1 Test Plan

**Test Categories:**

| Category | Description |
|----------|-------------|
| Functional | Features work correctly |
| Data | Migrated data is accurate |
| Integration | Systems work together |
| User Acceptance | Users can perform jobs |

### 9.2 Functional Test Cases

#### Journal Entry Tests

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Create basic JV | Create voucher with 2 entries | Voucher saved, balances |
| Multi-line JV | Create voucher with 10+ entries | All entries saved |
| Edit existing JV | Modify entry amount | Changes saved |
| Delete JV | Delete voucher | Voucher removed |

#### Delivery Challan Tests

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Create DC | Process DC from voucher | DC created, entry processed |
| Partial DC | Process partial quantity | Remaining qty tracked |
| Batch DC | Process multiple entries | All entries processed |
| FOR Charges | Add FOR to DC | FOR voucher created |
| Cartage | Add cartage to DC | Cartage voucher created |

#### Report Tests

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Aging Report | Generate aging | Balances by age shown |
| Master Report | Generate ledger | Transactions with running balance |
| Account Report | Generate for account | Account transactions listed |
| Product Report | Generate product | Qty and values calculated |

### 9.3 User Acceptance Testing

**UAT Process:**
1. Prepare test scenarios based on real workflows
2. Assign test cases to users
3. Execute tests in staging environment
4. Document results and issues
5. Resolve critical issues
6. Sign-off on UAT completion

**UAT Sign-off Criteria:**
- All critical workflows tested
- No critical defects unresolved
- Performance acceptable
- Data accuracy verified

---

## 10. Go-Live Checklist

### 10.1 Pre-Go-Live (T-7 Days)

- [ ] UAT sign-off obtained
- [ ] All users created and permissions set
- [ ] Training completed
- [ ] Data migration validated
- [ ] Backup procedures tested
- [ ] Support contacts documented

### 10.2 Cutover Day (T-0)

- [ ] Stop transactions in old system
- [ ] Final data export from old system
- [ ] Import any pending transactions
- [ ] Verify final balances
- [ ] Switch DNS (if applicable)
- [ ] Announce go-live to users
- [ ] Monitor for issues

### 10.3 Post-Go-Live (T+1 to T+14)

- [ ] Daily check-ins with users
- [ ] Issue tracking and resolution
- [ ] Performance monitoring
- [ ] User feedback collection
- [ ] Documentation updates

---

## 11. Training Program

### 11.1 Training Modules

#### Module 1: System Navigation (1 hour)
- Login and authentication
- Dashboard overview
- Menu navigation
- Basic UI concepts

#### Module 2: Journal Entries (2 hours)
- Creating vouchers
- Entry types (Account vs Product)
- Editing and deleting
- Searching vouchers

#### Module 3: Delivery Challans (2 hours)
- Processing single DC
- Batch processing
- FOR charges
- Cartage processing
- Partial deliveries

#### Module 4: Base Vouchers (1 hour)
- Creating base vouchers
- Derived voucher processing
- Full processing
- Use cases

#### Module 5: Reports (2 hours)
- Aging Report
- Master Report
- Account Report
- Product Report
- User Report
- Dashboard

#### Module 6: Administration (1 hour)
- User management
- Permission configuration
- Project settings
- System maintenance

### 11.2 Training Schedule

| Audience | Modules | Duration |
|----------|---------|----------|
| End Users | 1, 2, 3, 5 | 7 hours |
| Power Users | 1, 2, 3, 4, 5 | 8 hours |
| Administrators | 1, 2, 3, 4, 5, 6 | 9 hours |

### 11.3 Training Materials

| Material | Description |
|----------|-------------|
| User Guide | Step-by-step instructions |
| Quick Reference Card | Common tasks at a glance |
| Video Library | Screen recordings of key workflows |
| Practice Database | Sandbox for hands-on learning |

---

## 12. Post-Implementation Support

### 12.1 Support Channels

| Channel | Use Case | Response Time |
|---------|----------|---------------|
| Email | Non-urgent questions | 24 hours |
| Phone | Urgent issues | Same day |
| Knowledge Base | Self-service | Immediate |
| Scheduled Call | Training follow-up | By appointment |

### 12.2 Issue Escalation

| Severity | Description | Response |
|----------|-------------|----------|
| Critical | System down, data loss | 2 hours |
| High | Major feature broken | 4 hours |
| Medium | Feature issue, workaround exists | 24 hours |
| Low | Enhancement request, minor issue | 48 hours |

### 12.3 Ongoing Optimization

**Monthly Review:**
- Usage statistics
- Performance metrics
- User feedback
- Feature requests

**Quarterly Business Review:**
- ROI assessment
- Process improvement opportunities
- Roadmap alignment
- Training refresher needs

---

## 13. Appendices

### Appendix A: Excel Import Templates

**Account Import Template:**
```
Account Name | Category | Account Group | Phone | Address | City
ACME Corp    | Customers| Account       | 123-456 | 123 Main St | Karachi
Widget Co    | Suppliers| Account       | 789-012 | 456 Oak Ave | Lahore
Product A    | Products | Product       |         |             |
```

### Appendix B: Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Login fails | Incorrect credentials | Reset password |
| Permission denied | Missing permission | Admin adds permission |
| Voucher won't balance | Entry error | Check debit/credit totals |
| DC not processing | Entry already processed | Check entry status |
| Report empty | Filter too restrictive | Adjust date/filters |

### Appendix C: Glossary

| Term | Definition |
|------|------------|
| JV | Journal Voucher - a transaction record |
| DC | Delivery Challan - proof of delivery |
| FOR | Freight On Road - transportation charges |
| RBAC | Role-Based Access Control |
| Base Voucher | Template for repetitive transactions |
| Derived Voucher | Created from base voucher |

### Appendix D: Contact Information

| Contact | Details |
|---------|---------|
| Implementation Support | implement@pureledger.com |
| Technical Support | support@pureledger.com |
| Training | training@pureledger.com |
| Sales | sales@pureledger.com |

---

**Document Version:** 1.0
**Last Updated:** January 2026
**Document Owner:** Customer Success Team
