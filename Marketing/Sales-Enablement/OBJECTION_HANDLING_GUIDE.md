# PureLedger Objection Handling Guide

**Responses to Common Sales Objections**

---

## Quick Reference

| Objection | Short Response |
|-----------|----------------|
| Too expensive | "It's actually cheaper than the hidden costs you're paying now" |
| Happy with current system | "That makes sense - but is it handling DCs and freight allocation?" |
| We're too small | "Our Starter plan is designed exactly for businesses your size" |
| Implementation too complex | "Typical implementation is 4-6 weeks with minimal disruption" |
| Security concerns | "We use the same security standards as major banks" |
| Need more features | "Which specific features? We may have them or they're on our roadmap" |

---

## Detailed Objection Responses

### 1. Price Objections

---

#### "It's too expensive"

**ACKNOWLEDGE:**
> "I understand budget is a consideration. Can I ask what you're comparing it to?"

**REFRAME:**
> "Let me share what customers in similar situations have found. When they add up the real costs of their current approach - the hours spent on reconciliation, the freight that gets misallocated, the credit violations from outdated information - the total cost is usually much higher than our subscription."

**QUANTIFY:**
> "Based on what you've told me about doing [X] transactions monthly, companies your size typically spend 15-20 hours per month on DC reconciliation alone. At even a modest hourly rate, that's $500-800/month in labor - more than our Professional plan costs."

**CLOSE:**
> "Would it help if I walked through the ROI calculator with your specific numbers?"

**SUPPORTING MATERIALS:**
- ROI Calculator Guide
- Customer testimonials with cost savings

---

#### "Competitor X is cheaper"

**ACKNOWLEDGE:**
> "You're right that there are cheaper options out there. Can I ask which one you're looking at?"

**DIFFERENTIATE:**
> "The key question is: do they do what you actually need? Most accounting software doesn't have native DC processing, automatic freight allocation, or the kind of reporting you've described needing. If you have to do all that manually, the cheaper software actually costs more in labor."

**SPECIFICS (by competitor):**

**QuickBooks:**
> "QuickBooks is great general-purpose software, but it wasn't built for trading businesses. It doesn't have delivery challans, FOR charges, or base vouchers. You'd end up managing all that in spreadsheets alongside QuickBooks."

**Tally:**
> "Tally is powerful, but it's primarily designed for Indian GST compliance. The DC tracking is an add-on, not built in. And if you need multi-company or multi-project, you're looking at their Gateway product, which has its own learning curve."

**Zoho Books:**
> "Zoho is affordable, but again, it's general-purpose. No DC integration, no freight allocation, no base voucher system. For a trading business, you'd be using it as just a ledger and doing everything else manually."

**CLOSE:**
> "I'd be happy to do a side-by-side feature comparison if that would help."

---

#### "Can we get a discount?"

**ACKNOWLEDGE:**
> "I appreciate you asking. Our pricing is designed to be fair and competitive."

**OPTIONS:**
> "Here's what I can offer:
> - Annual billing saves you 2 months (about 17% discount)
> - If you're planning to add users later, starting with a larger plan now locks in better per-user pricing
> - For multi-year commitments, we can discuss additional discounts"

**IF PUSHING FOR MORE:**
> "I can check with my manager on a pilot discount for the first quarter if you're ready to commit today."

---

### 2. Timing/Priority Objections

---

#### "We're not ready to switch right now"

**ACKNOWLEDGE:**
> "I understand. Change takes planning. Can I ask what's holding you back?"

**IF BUSY SEASON:**
> "That makes sense. When would be a better time? I can send you some materials to review and we can reconnect when your schedule opens up."

**IF NO BUDGET:**
> "When does your next budget cycle start? I can prepare a business case you can present internally."

**IF INERTIA:**
> "I get it - switching systems feels like a big project. But here's what I've seen: the longer you wait, the more manual reconciliation hours you accumulate. What if we did a small pilot with just one project to prove the value?"

**CLOSE:**
> "Would it make sense to start a free trial now, so when you are ready, you'll already know the system?"

---

#### "We just switched to [competitor] recently"

**ACKNOWLEDGE:**
> "I understand - nobody wants to switch systems twice. How's that working out for you?"

**EXPLORE:**
> "Are they handling the DC reconciliation and freight allocation you mentioned? How much time is that still taking?"

**PLANT SEED:**
> "I'd suggest staying in touch. In 6 months, if you're still dealing with manual reconciliation, we'll be here. Can I add you to our newsletter so you see what we're building?"

---

### 3. Feature/Capability Objections

---

#### "Does it do [specific feature]?"

**IF YES:**
> "Yes, absolutely. Let me show you how that works." [Demo if possible]

**IF NO BUT PLANNED:**
> "That's on our roadmap for [timeframe]. Here's how customers handle it today: [workaround]. Would that work for you?"

**IF NO AND NOT PLANNED:**
> "That's not something we currently have. Can you tell me more about how you'd use it? This is valuable feedback for our product team."

**REDIRECT:**
> "The features you mentioned earlier - DC processing, freight allocation, running balances - those are our core strengths. Does [feature] matter more than those?"

---

#### "We need mobile apps"

**ACKNOWLEDGE:**
> "Mobile access is increasingly important. Here's where we are:"

**RESPOND:**
> "Our web application is fully responsive - it works great on phone and tablet browsers. You can create vouchers, process DCs, and run reports from your phone today.
>
> Dedicated native apps are on our roadmap. We're building them with offline capability, which takes time to do right."

**CLOSE:**
> "Would the responsive web version work for your mobile needs in the meantime?"

---

#### "We need [tax compliance feature]"

**PAKISTAN/FBR:**
> "We support FBR integration for Pakistani businesses, including NTN validation."

**INDIA/GST:**
> "GST compliance is on our roadmap as we expand into the Indian market."

**UAE/VAT:**
> "Our reporting supports VAT requirements. We can configure tax tracking as needed."

**GENERAL:**
> "What specific compliance requirement do you have? We've worked with customers in similar regulatory environments and can often configure the system to meet those needs."

---

### 4. Trust/Risk Objections

---

#### "We've never heard of you"

**ACKNOWLEDGE:**
> "That's fair - we're not a household name like QuickBooks or Tally."

**ESTABLISH CREDIBILITY:**
> "We've been deployed at trading companies for [X] years. We're built on proven technology - .NET Core and PostgreSQL, the same stack used by major enterprises.
>
> We're focused specifically on trading and distribution businesses. That's why we have features like DC integration that the big players don't."

**SOCIAL PROOF:**
> "Would it help to speak with a current customer in a similar industry?"

**REDUCE RISK:**
> "You can start with a 14-day free trial - no credit card, no commitment. That gives you time to verify everything works before you decide."

---

#### "What happens if you go out of business?"

**ACKNOWLEDGE:**
> "That's a smart question to ask any software vendor."

**RESPOND:**
> "Here's what we offer:
> 1. Your data is always exportable - you're never locked in
> 2. For self-hosted deployments, you have complete control
> 3. We're a growing business with [funding/revenue/customer] traction
>
> But I understand the concern. Would a trial period help you evaluate the risk?"

---

#### "Is our data secure?"

**ACKNOWLEDGE:**
> "Absolutely critical question, especially for financial data."

**RESPOND:**
> "Our security includes:
> - **Encryption in transit:** All data over HTTPS/TLS
> - **Authentication:** JWT tokens with BCrypt password hashing
> - **Access control:** Granular permissions at project, category, and action levels
> - **Data isolation:** Each customer's data is separated
>
> We have a security white paper I can share that details our full security architecture."

**CLOSE:**
> "What specific security requirements does your organization have? We can address those directly."

---

### 5. Process/Change Objections

---

#### "Implementation seems complicated"

**ACKNOWLEDGE:**
> "I understand - system changes can feel daunting."

**SIMPLIFY:**
> "Let me break it down. A typical implementation is 4-6 weeks:
> - Week 1-2: Setup and configuration
> - Week 2-3: Data migration
> - Week 4: Training
> - Week 5-6: Go-live and stabilization
>
> You don't need to do it all at once. Many customers start with one project and expand from there."

**SUPPORT:**
> "We provide implementation support throughout. You'll have a dedicated contact who handles configuration, data migration, and training."

**REDUCE FRICTION:**
> "What specifically concerns you about implementation? Let's address that directly."

---

#### "Our team won't adopt it"

**ACKNOWLEDGE:**
> "User adoption is critical to getting value from any system."

**RESPOND:**
> "Here's what we've found helps:
> 1. **Involve users early** - Get their input during the trial
> 2. **Training** - We provide role-specific training
> 3. **Quick wins** - Start with their biggest pain point (usually reconciliation)
> 4. **Support** - We're available to help users who get stuck
>
> Once they see the DC-invoice matching happen automatically, adoption usually isn't a problem."

**CLOSE:**
> "Would it help to include key users in the demo or trial?"

---

#### "We're happy with our current system"

**ACKNOWLEDGE:**
> "That's great - a working system is valuable."

**EXPLORE:**
> "Can I ask: How are you handling DC-to-invoice reconciliation currently? And freight allocation?"

**IF MANUAL PROCESSES REVEALED:**
> "So it sounds like you're doing that work outside the system. That's exactly the gap PureLedger fills. We bring those processes into the accounting workflow so they're always in sync."

**IF TRULY SATISFIED:**
> "I appreciate that. Would it be okay if I sent some materials about our approach? If things change or you hear about DC reconciliation problems from colleagues, you'll know there's a solution."

---

## Objection Prevention

The best way to handle objections is to prevent them. During discovery:

1. **Understand their pain** - Ask about specific problems, not just current systems
2. **Quantify the cost** - Get numbers on reconciliation time, errors, etc.
3. **Identify decision criteria** - What matters most: price, features, support?
4. **Surface concerns early** - Ask "What would make you hesitate to move forward?"

---

## When to Walk Away

Not every prospect is a fit. Signs this isn't the right customer:

- No delivery challan/logistics needs (we're not a general accounting tool)
- Extremely price-sensitive with no value awareness
- Needs extensive customization we can't provide
- Timeline is "someday" with no urgency
- Hostile to change despite clear pain

**Response:**
> "Based on our conversation, it sounds like [generic accounting tool] might actually be a better fit for your needs. If your situation changes and you start dealing with DC reconciliation challenges, we'd be happy to reconnect."

---

**Document Version:** 1.0
**Last Updated:** January 2026
**For Internal Sales Use Only**
