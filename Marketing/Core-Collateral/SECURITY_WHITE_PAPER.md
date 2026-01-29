# PureLedger Security White Paper

**Enterprise-Grade Security for Financial Data Protection**

**Version 1.0 | January 2026**

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Security Architecture Overview](#2-security-architecture-overview)
3. [Authentication System](#3-authentication-system)
4. [Authorization & Access Control](#4-authorization--access-control)
5. [Data Protection](#5-data-protection)
6. [Application Security](#6-application-security)
7. [Infrastructure Security](#7-infrastructure-security)
8. [Operational Security](#8-operational-security)
9. [Compliance & Certifications](#9-compliance--certifications)
10. [Security Roadmap](#10-security-roadmap)
11. [Appendices](#11-appendices)

---

## 1. Executive Summary

PureLedger is designed with security as a foundational principle, not an afterthought. As an enterprise accounting and logistics platform handling sensitive financial data, we implement multiple layers of protection across authentication, authorization, data handling, and infrastructure.

### Security Highlights

| Layer | Implementation |
|-------|----------------|
| **Authentication** | JWT tokens with BCrypt password hashing |
| **Authorization** | Granular role-based access control (RBAC) |
| **Data in Transit** | HTTPS/TLS 1.2+ encryption |
| **Data at Rest** | Database encryption (when deployed on encrypted storage) |
| **Application** | Input validation, parameterized queries, secure coding |
| **Infrastructure** | Isolated environments, firewall protection |

### Security Principles

1. **Defense in Depth** - Multiple security layers, no single point of failure
2. **Least Privilege** - Users access only what they need
3. **Secure by Default** - Authentication required, permissions denied unless granted
4. **Transparency** - Clear documentation of security measures
5. **Continuous Improvement** - Regular updates and security enhancements

---

## 2. Security Architecture Overview

### System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      INTERNET                                │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTPS (TLS 1.2+)
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   LOAD BALANCER / REVERSE PROXY             │
│                   (SSL Termination, Rate Limiting)          │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   APPLICATION TIER                          │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │  React Frontend  │  │  ASP.NET Core    │                │
│  │  (Static Files)  │  │  Backend API     │                │
│  └──────────────────┘  └────────┬─────────┘                │
│                                 │                           │
│           ┌─────────────────────┼─────────────────────┐     │
│           │  MIDDLEWARE PIPELINE                      │     │
│           │  ┌─────────────────────────────────────┐  │     │
│           │  │ Exception Handler                   │  │     │
│           │  │ Authentication (JWT)                │  │     │
│           │  │ Authorization (RBAC)                │  │     │
│           │  │ CORS Policy                         │  │     │
│           │  │ Input Validation                    │  │     │
│           │  └─────────────────────────────────────┘  │     │
│           └───────────────────────────────────────────┘     │
└─────────────────────────┬───────────────────────────────────┘
                          │ Parameterized Queries
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   DATA TIER                                 │
│  ┌──────────────────────────────────────────────────────┐  │
│  │                 PostgreSQL Database                   │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐    │  │
│  │  │ User Data   │ │ Financial   │ │ Audit       │    │  │
│  │  │ (Hashed)    │ │ Records     │ │ Logs        │    │  │
│  │  └─────────────┘ └─────────────┘ └─────────────┘    │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Security Boundaries

| Boundary | Protection |
|----------|------------|
| Internet → Application | HTTPS, Rate Limiting, CORS |
| Frontend → API | JWT Authentication, CSRF Protection |
| API → Database | Parameterized Queries, Connection Encryption |
| User → Data | RBAC, Project/Category Permissions |

---

## 3. Authentication System

### 3.1 JWT Token Authentication

PureLedger uses JSON Web Tokens (JWT) for stateless authentication.

**Token Structure:**
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "nameid": "123",           // User ID
    "unique_name": "username", // Username
    "role": "Admin",           // User role
    "exp": 1704067200,         // Expiration timestamp
    "iss": "PureLedger",       // Issuer
    "aud": "PureLedgerUsers"   // Audience
  },
  "signature": "..."
}
```

**Token Validation Parameters:**

| Parameter | Setting | Purpose |
|-----------|---------|---------|
| ValidateIssuerSigningKey | True | Verify token signature |
| ValidateIssuer | True | Confirm token origin |
| ValidateAudience | True | Confirm intended recipient |
| ValidateLifetime | True | Check expiration |
| ClockSkew | Zero | Prevent timing attacks |

**Token Lifecycle:**

```
User Login
    │
    ▼
Credentials Validated (BCrypt)
    │
    ▼
JWT Token Generated
    │ Claims: UserId, Username, Role
    │ Expiry: Configurable (default 24 hours)
    │ Signature: HMAC-SHA256
    ▼
Token Returned to Client
    │
    ▼
Client Stores Token
    │
    ▼
Token Sent with Each Request
    │ Authorization: Bearer <token>
    ▼
Server Validates Token
    │
    ▼
Request Processed (if valid)
```

### 3.2 Password Security

**Hashing Algorithm:** BCrypt

BCrypt is an adaptive hashing function specifically designed for password storage.

**Properties:**
- **Salt:** Automatically generated and incorporated
- **Work Factor:** Adaptive (increases with hardware improvements)
- **Output:** 60-character hash string
- **Verification:** Constant-time comparison (prevents timing attacks)

**Implementation:**
```
Password → BCrypt.HashPassword() → Hashed Password (stored)
Login → BCrypt.Verify(input, stored) → True/False
```

**Why BCrypt:**
| Feature | BCrypt | MD5/SHA1 | SHA-256 |
|---------|--------|----------|---------|
| Designed for passwords | Yes | No | No |
| Built-in salt | Yes | No | No |
| Adaptive work factor | Yes | No | No |
| Timing attack resistant | Yes | No | No |

### 3.3 Session Management

**Token Storage (Client-side):**
- Stored in browser localStorage (configurable)
- Removed on logout
- Automatic redirect to login on token expiration

**Session Timeout:**
- Configurable token expiry (default: 24 hours)
- 401 responses trigger automatic logout
- No server-side session state (stateless architecture)

---

## 4. Authorization & Access Control

### 4.1 Role-Based Access Control (RBAC)

PureLedger implements a comprehensive RBAC system with multiple permission levels.

**Role Hierarchy:**

```
Admin User
    │
    ├── Full system access
    ├── User management
    ├── All projects and categories
    └── All controller actions

Regular User
    │
    ├── Project-level permissions
    │   └── Access only to assigned projects
    │
    ├── Category-level permissions
    │   └── Hidden categories filter data visibility
    │
    └── Action-level permissions
        └── Specific operations (Entry, Reports, etc.)
```

### 4.2 Permission Architecture

**Controller-Action Permission Mapping:**

| Controller | Action | Required Permissions |
|------------|--------|---------------------|
| Home | Entry | Entry |
| Home | VoucherEntry | VoucherEntry |
| Home | BaseVouchers | BaseVouchers |
| MetaData | Projects | Projects |
| MetaData | Categories | Categories |
| MetaData | Accounts | Accounts |
| DeliveryChallans | CreateDC | DeliveryChallans |
| DeliveryChallans | ProcessBatch | DeliveryChallans |
| Reporting | AgingReport | AgingReport |
| Reporting | AccountReport | AccountReport |
| Reporting | MasterReport | MasterReport |
| Reporting | ProductReport | ProductReport |
| Reporting | UserReport | UserReport |

### 4.3 Permission Checking Flow

```
Request Received
    │
    ▼
Authentication Check
    │ Is user authenticated?
    │ No → 401 Unauthorized
    ▼
Admin Check
    │ Is user admin?
    │ Yes → Access Granted
    ▼
Project Permission Check
    │ Does user have project access?
    │ No → 403 Forbidden
    ▼
Controller Permission Check
    │ Can user access this controller?
    │ No → 403 Forbidden
    ▼
Action Permission Check
    │ Can user perform this action?
    │ No → 403 Forbidden
    ▼
Access Granted
```

### 4.4 Category-Level Data Filtering

Beyond access permissions, PureLedger supports data-level filtering:

**Hidden Categories:**
- Admins can mark categories as hidden from specific users
- Reports automatically filter out hidden category data
- Users cannot see or query hidden category information

**User Report Restrictions:**
- User Report validates category access before execution
- Hidden categories throw UnauthorizedAccessException
- Data filtering applied at query level

---

## 5. Data Protection

### 5.1 Data in Transit

**Protocol:** HTTPS with TLS 1.2 or higher

**Implementation:**
- All API endpoints require HTTPS in production
- HTTP requests redirected to HTTPS
- Secure headers enforced

**Cipher Suites:**
Modern, secure cipher suites only (configurable at infrastructure level)

### 5.2 Data at Rest

**Database Security:**
- PostgreSQL with connection encryption
- Sensitive fields (passwords) hashed before storage
- Database access restricted to application service account

**Backup Security:**
- Encrypted backup storage (infrastructure dependent)
- Access-controlled backup locations
- Regular backup verification

### 5.3 Sensitive Data Handling

| Data Type | Protection |
|-----------|------------|
| Passwords | BCrypt hashed (never stored plain) |
| JWT Secrets | Environment variables (not in code) |
| Database credentials | Configuration secrets (not in source) |
| API tokens | Short-lived, regularly rotated |
| Financial data | Access-controlled, audit logged |

---

## 6. Application Security

### 6.1 Input Validation

**Server-Side Validation:**
- Model validation attributes on all input models
- Required fields enforced
- Range validation for numeric fields
- String length limits

**Example:**
```
CartageViewModel
├── JournalVoucherId: Required
├── EntryId: Required
├── TransporterId: Required
├── Amount: Required, Range(1, MaxValue)
├── DcDate: Required
├── DcLocation: Required
└── ForAmount: Range(0, MaxValue)
```

### 6.2 SQL Injection Prevention

**Approach:** Entity Framework Core with parameterized queries

All database access goes through Entity Framework Core, which:
- Parameterizes all queries automatically
- Escapes special characters
- Prevents SQL injection by design

**Example Query Pattern:**
```csharp
// Safe: Parameterized
var account = await _context.Accounts
    .Where(a => a.AccountId == accountId)
    .FirstOrDefaultAsync();

// Never used: String concatenation
// var account = _context.Accounts.FromSqlRaw($"SELECT * FROM Accounts WHERE Id = {id}");
```

### 6.3 Cross-Site Scripting (XSS) Prevention

**Frontend:**
- React automatically escapes rendered content
- No `dangerouslySetInnerHTML` without sanitization
- Content Security Policy headers (infrastructure)

**Backend:**
- JSON serialization escapes special characters
- No HTML rendering in API responses

### 6.4 Cross-Origin Resource Sharing (CORS)

**Configuration:**
- Explicit origin allowlist (no wildcards)
- Credentials required
- Methods and headers restricted

**Allowed Origins:**
Configured per environment, not hardcoded.

### 6.5 Error Handling

**Development vs. Production:**

| Environment | Error Response |
|-------------|----------------|
| Development | Full stack trace, detailed message |
| Production | Generic message, no internal details |

**Exception Handling:**
- Global exception handler middleware
- Errors logged server-side
- Safe error messages returned to client

---

## 7. Infrastructure Security

### 7.1 Deployment Architecture

**Cloud Hosted (Recommended):**
- Isolated virtual networks
- Firewall rules limit inbound traffic
- Database not directly accessible from internet
- Load balancing for availability

**Self-Hosted:**
- Customer-managed infrastructure
- Security configuration guidance provided
- Regular security update advisories

### 7.2 Network Security

| Layer | Protection |
|-------|------------|
| Perimeter | Firewall, DDoS protection |
| Application | WAF rules, rate limiting |
| Database | Internal network only, no public IP |
| Management | VPN/bastion for admin access |

### 7.3 Environment Isolation

**Separation:**
- Development, staging, and production environments isolated
- No shared credentials between environments
- Production data never used in non-production

---

## 8. Operational Security

### 8.1 Monitoring & Logging

**Application Logging:**
- All API requests logged
- Authentication events logged
- Errors logged with context
- No sensitive data in logs

**Log Retention:**
- Configurable retention period
- Secure log storage
- Access-controlled log viewing

### 8.2 Incident Response

**Process:**
1. Detection - Monitoring alerts or user report
2. Triage - Severity assessment
3. Containment - Isolate affected systems
4. Investigation - Root cause analysis
5. Remediation - Fix and verify
6. Communication - Notify affected parties
7. Post-mortem - Document and improve

**Contact:**
Security incidents: security@pureledger.com

### 8.3 Vulnerability Management

**Process:**
- Regular dependency updates
- Security advisory monitoring
- Penetration testing (annual)
- Bug bounty program (planned)

---

## 9. Compliance & Certifications

### 9.1 Current Status

| Standard | Status |
|----------|--------|
| GDPR | Compliant practices implemented |
| SOC 2 | Roadmap |
| ISO 27001 | Roadmap |
| PCI DSS | Not applicable (no card processing) |

### 9.2 Data Privacy

**Data Collection:**
- Only data necessary for service operation
- No third-party tracking
- User data deletion on request

**Data Location:**
- Configurable data residency
- Regional deployment options available

### 9.3 Audit Trail

**Tracked Events:**
- User authentication
- Record creation/modification/deletion
- Permission changes
- System configuration changes

---

## 10. Security Roadmap

### Planned Enhancements

| Feature | Priority | Timeline |
|---------|----------|----------|
| Refresh token implementation | High | Q1 2026 |
| HttpOnly cookie token storage | High | Q1 2026 |
| Session timeout configuration | Medium | Q1 2026 |
| Rate limiting implementation | Medium | Q2 2026 |
| Two-factor authentication | High | Q2 2026 |
| Account lockout policy | Medium | Q2 2026 |
| Security headers middleware | Medium | Q2 2026 |
| CSRF token implementation | Medium | Q3 2026 |
| SOC 2 certification | High | Q4 2026 |
| Audit logging enhancement | Medium | Q3 2026 |

---

## 11. Appendices

### A. Security Configuration Checklist

**For Self-Hosted Deployments:**

- [ ] HTTPS enabled with valid certificate
- [ ] JWT secret stored in environment variable (not code)
- [ ] Database credentials secured
- [ ] CORS origins restricted to your domains
- [ ] Firewall rules configured
- [ ] Regular backups enabled
- [ ] Monitoring configured
- [ ] Log retention policy set
- [ ] Update schedule established

### B. Security Contacts

| Purpose | Contact |
|---------|---------|
| Security incidents | security@pureledger.com |
| Vulnerability reports | security@pureledger.com |
| Compliance questions | compliance@pureledger.com |
| General security questions | support@pureledger.com |

### C. Document Control

| Item | Value |
|------|-------|
| Document Owner | Security Team |
| Classification | Public |
| Version | 1.0 |
| Last Updated | January 2026 |
| Next Review | July 2026 |

---

**Disclaimer:** This document describes the security architecture and practices of PureLedger as of the publication date. Security is an ongoing process, and specific implementations may be updated to address new threats or requirements. For the most current security information, contact security@pureledger.com.
