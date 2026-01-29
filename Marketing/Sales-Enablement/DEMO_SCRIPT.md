# PureLedger Demo Script

**Standard 30-Minute Product Demonstration**

---

## Pre-Demo Preparation

### Discovery Questions (Ask Before Demo)

1. What business are you in? (Trading, distribution, manufacturing, etc.)
2. How many monthly transactions do you process?
3. What system do you currently use?
4. Do you process delivery challans?
5. Do you have FOR or cartage charges?
6. How many users would need access?
7. What's your biggest pain point with current system?

### Environment Setup

- [ ] Log into demo environment
- [ ] Clear previous demo data if needed
- [ ] Have sample vouchers ready
- [ ] Prepare industry-relevant accounts and products
- [ ] Test screen sharing

---

## Demo Flow (30 Minutes)

### Opening (2 minutes)

**SAY:**
> "Thanks for joining today. Based on our conversation, I understand you're a [INDUSTRY] business doing about [VOLUME] transactions monthly, and your biggest challenge is [PAIN POINT].
>
> I'm going to show you how PureLedger specifically addresses that. I'll demonstrate the core workflow, then we'll have time for questions. Sound good?"

### Section 1: System Overview (3 minutes)

**SHOW:** Dashboard

**SAY:**
> "This is the PureLedger dashboard. It gives you a real-time view of your business - voucher counts, completion status, and recent activity. Notice how you can filter by project if you manage multiple business units."

**HIGHLIGHT:**
- Project selector
- Voucher statistics
- Completion rates
- Date range filters

**TRANSITION:**
> "Let me show you where the real magic happens - creating transactions."

---

### Section 2: Journal Entry Creation (5 minutes)

**NAVIGATE:** Home > Entry

**SAY:**
> "This is where you create journal entries. PureLedger supports both account entries - for customers, suppliers, banks - and product entries with quantity tracking. Let me create a typical sale."

**DEMO:** Create a sale voucher

1. Select Project
2. Set Entry Date
3. Add Customer (Account entry with debit)
4. Add Products (Product entries with credit, quantity, rate)
5. Show automatic balance calculation

**SAY:**
> "Notice how the system automatically calculates the balance. You can't save an unbalanced voucher - that's double-entry integrity built in."

**HIGHLIGHT:**
- Auto-balance calculation
- Product quantity tracking
- Rate and discount fields
- Multi-line capability

**IF ASKED about recipes:**
> "For repetitive transactions, we have recipes - saved templates that let you create complex vouchers with one click. Very useful for manufacturing or recurring orders."

---

### Section 3: Delivery Challan Processing (7 minutes)

**[THIS IS THE KEY DIFFERENTIATOR - SPEND TIME HERE]**

**NAVIGATE:** DC Processing

**SAY:**
> "Now here's what makes PureLedger different from every other accounting system. Watch this."

**DEMO:** Process a DC from the voucher just created

1. Select the voucher
2. Show product entries waiting to be processed
3. Click to process DC
4. Set location, station, reference
5. Process the DC

**SAY:**
> "The delivery challan and the accounting entry are now linked. There's no reconciliation needed at month-end because they were never separate in the first place. In a typical trading company, this alone saves 20+ hours per month."

**DEMO:** Show the voucher is now marked as processed

**SAY:**
> "See how the voucher status changed? The system knows this has been delivered."

**IF TIME:** Show partial delivery

**SAY:**
> "What if you only deliver part of the order? Watch this..."

1. Create voucher with quantity 100
2. Process DC for 60
3. Show remaining 40 as unprocessed

**SAY:**
> "The remaining quantity is tracked automatically. No spreadsheets, no manual notes."

---

### Section 4: FOR and Cartage (3 minutes)

**[INCLUDE IF PROSPECT HAS FREIGHT CHARGES]**

**DEMO:** Process DC with FOR charges

1. Select voucher
2. Enable FOR charges option
3. Set FOR amount
4. Show automatic voucher creation

**SAY:**
> "See that? The FOR charge was automatically posted. If you chose 'Post to Customer,' it would have been added to their balance. No manual journal entry needed."

**DEMO:** Show cartage processing if time permits

---

### Section 5: Reporting (5 minutes)

**NAVIGATE:** Reporting > Master Report

**SAY:**
> "Let's talk about reporting. The Master Report is your complete ledger with running balances."

**DEMO:** Generate Master Report with filters

**HIGHLIGHT:**
- Date range filtering
- Project/Category/Account filters
- Running balance column
- Export capability

**NAVIGATE:** Aging Report

**SAY:**
> "The Aging Report shows you who owes what and for how long. Notice it uses the creditable date, not just the transaction date - so if you gave 30-day terms, it ages from when payment was actually due."

**NAVIGATE:** Product Report

**SAY:**
> "The Product Report shows inventory positions and profitability. You get quantity balances, average cost, revenue, COGS, and margin - all calculated automatically."

---

### Section 6: Access Control (3 minutes)

**[INCLUDE FOR LARGER PROSPECTS]**

**SAY:**
> "For larger teams, access control matters. Let me show you how granular PureLedger's permissions are."

**NAVIGATE:** Admin > Users (or describe if demo user is not admin)

**SAY:**
> "You can control access at three levels:
> 1. Which projects a user can see
> 2. Which categories are hidden from them
> 3. Which specific actions they can perform
>
> So your sales team might only see their own customers, your accountants see everything, and branch managers see only their branch."

---

### Section 7: Summary and Differentiators (2 minutes)

**SAY:**
> "Let me summarize what makes PureLedger different:
>
> 1. **Native DC Integration** - Delivery challans are part of the accounting workflow, not a separate system
>
> 2. **Automatic Freight Allocation** - FOR and cartage charges post automatically to the right accounts
>
> 3. **Real-Time Visibility** - Customer balances, inventory positions, and delivery status are always current
>
> 4. **Trading-Specific Reports** - Built for how trading businesses actually operate
>
> 5. **Granular Access Control** - Everyone sees exactly what they need
>
> Does this address the [PAIN POINT] you mentioned earlier?"

---

### Closing (2 minutes)

**SAY:**
> "What questions do you have?"

**HANDLE QUESTIONS**

**NEXT STEPS:**
> "Based on what you've seen, I think PureLedger would be a great fit for [COMPANY]. Here's what I suggest:
>
> 1. Start a 14-day free trial so you can test with your own data
> 2. I'll send you a pricing proposal based on [USER COUNT] users
> 3. Let's schedule a follow-up next week to address any questions
>
> Does that work?"

---

## Industry Variations

### For Trading/Distribution
- Emphasize DC-Invoice reconciliation savings
- Show partial delivery tracking
- Highlight aging with creditable dates

### For Manufacturing
- Demonstrate recipes for BOM
- Show product quantity tracking
- Highlight COGS calculation

### For Import/Export
- Emphasize multi-project for shipments
- Show comprehensive reporting
- Highlight location tracking

### For Multi-Location Retail
- Show multi-project for branches
- Demonstrate permission controls
- Highlight consolidated reporting

### For Textile/Garment
- Demonstrate base vouchers for orders
- Show derived voucher creation
- Highlight partial shipment tracking

---

## Common Demo Questions & Answers

**Q: Can we import our existing data?**
> "Yes, we support Excel imports for accounts and opening balances. For historical transactions, we offer a migration service."

**Q: Is it cloud-based or can we self-host?**
> "Both. Most customers use our cloud hosting for convenience, but we offer self-hosted deployment for customers with specific requirements."

**Q: What about mobile access?**
> "The system is fully responsive - it works on mobile browsers. Dedicated mobile apps are on our roadmap."

**Q: How long does implementation take?**
> "Typically 4-6 weeks from start to go-live, depending on data migration complexity."

**Q: What happens if we have problems?**
> "We offer email support with 24-hour response for standard plans, and phone support for premium plans. Plus, you get a dedicated success manager during implementation."

---

## Demo Don'ts

- Don't read from a script - use this as a guide
- Don't show every feature - focus on their pain points
- Don't ignore questions - address them when asked
- Don't rush the DC processing section - this is the differentiator
- Don't forget to ask for the next step

---

## Post-Demo Actions

- [ ] Send thank you email within 2 hours
- [ ] Attach relevant one-pager for their industry
- [ ] Include pricing proposal
- [ ] Provide trial sign-up link
- [ ] Schedule follow-up call
- [ ] Update CRM with demo notes

---

**Document Version:** 1.0
**Last Updated:** January 2026
**For Internal Sales Use Only**
