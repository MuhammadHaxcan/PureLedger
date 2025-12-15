# PureLedger Application Security Analysis

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Backend Security Implementation](#backend-security-implementation)
3. [Frontend Security Implementation](#frontend-security-implementation)
4. [Critical Vulnerabilities](#critical-vulnerabilities)
5. [Security Issues by Priority](#security-issues-by-priority)
6. [Positive Security Findings](#positive-security-findings)
7. [Recommendations](#recommendations)
8. [Key Files Reference](#key-files-reference)

---

## Executive Summary

This document provides a comprehensive security analysis of the PureLedger application, covering both the backend (ASP.NET Core) and frontend (React) implementations. The application uses JWT-based authentication with role-based access control. While the foundation is solid, several critical issues need addressing before production deployment.

### Overall Security Score: **6.5/10**

| Category | Status |
|----------|--------|
| Authentication | Good (BCrypt + JWT) |
| Authorization | Good (RBAC implemented) |
| Token Storage | **Critical Issue** |
| Configuration Security | **Critical Issue** |
| Input Validation | Moderate |
| CORS | Needs Improvement |

---

## Backend Security Implementation

### 1. JWT Authentication

**Implementation Files:**
- `Pureledger-Backend/Services/JwtService.cs` (lines 1-83)
- `Pureledger-Backend/Program.cs` (lines 148-171)
- `Pureledger-Backend/Controllers/Api/AccountApiController.cs` (lines 29-88)

**How It Works:**

```csharp
// JwtService.cs - Token Generation (lines 15-47)
public string GenerateToken(User user)
{
    var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
    var issuer = _configuration["Jwt:Issuer"];
    var audience = _configuration["Jwt:Audience"];
    var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
    };

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.Now.AddMinutes(expiryMinutes),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**JWT Validation Configuration (Program.cs lines 148-171):**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero  // Prevents timing attacks
        };
    });
```

**Configuration (appsettings.json):**
```json
"Jwt": {
    "Issuer": "PureLedger",
    "Audience": "PureLedgerUsers",
    "SecretKey": "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLongForProduction!",
    "ExpiryMinutes": "1440"
}
```

---

### 2. Password Hashing

**File:** `Pureledger-Backend/Services/AuthService.cs`

**Implementation:**
```csharp
// Password hashing during registration (line 108)
PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),

// Password verification during login (line 63)
return await Task.FromResult(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
```

**Status:** BCrypt is properly implemented - this is a secure, adaptive hashing algorithm.

---

### 3. Authorization & Role-Based Access Control

**Files:**
- `Pureledger-Backend/Attributes/ControllerAccessAttribute.cs` (lines 1-343)
- `Pureledger-Backend/Program.cs` (lines 197-208)

**Global Authorization Policy (Program.cs):**
```csharp
// Lines 200-207 - Requires authentication for all API endpoints
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
```

**Custom Authorization Attribute Flow:**
```
1. User makes request to API endpoint
2. ControllerAccessAttribute.OnAuthorization() is invoked
3. Checks if user is authenticated
4. If Admin (user.IsAdmin) -> Access granted to everything
5. If regular user:
   a. Verifies project-level access
   b. Checks controller-level permissions
   c. Checks action-level permissions
6. Returns 403 Forbidden if access denied
```

**Permission Mapping (ControllerAccessAttribute.cs lines 46-129):**
```csharp
private static readonly Dictionary<string, List<string>> ActionPermissionMap = new()
{
    // Home Controller
    { "Home.Entry", new List<string> { "Entry" } },
    { "Home.VoucherEntry", new List<string> { "VoucherEntry" } },
    { "Home.BaseVouchers", new List<string> { "BaseVouchers" } },

    // MetaData Controller
    { "MetaData.Projects", new List<string> { "Projects" } },
    { "MetaData.Categories", new List<string> { "Categories" } },
    { "MetaData.Accounts", new List<string> { "Accounts" } },

    // Reporting Controller
    { "Reporting.SingleDoc", new List<string> { "SingleDoc" } },
    { "Reporting.AgingReport", new List<string> { "AgingReport" } },
    { "Reporting.AccountReport", new List<string> { "AccountReport" } },
    // ... more mappings
};
```

---

### 4. CORS Configuration

**File:** `Pureledger-Backend/Program.cs` (lines 71-146)

**Configuration:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

**Allowed Origins (appsettings.json):**
```json
"Cors": {
    "AllowedOrigins": [
        "http://localhost:5173",
        "http://localhost:5174",
        "http://localhost:3000",
        "https://localhost:5173",
        "https://localhost:5174",
        "http://72.61.118.244",
        "http://72.61.118.244:5173"
    ],
    "AllowCredentials": true
}
```

---

### 5. Exception Handling Middleware

**File:** `Pureledger-Backend/Middleware/GlobalExceptionHandlerMiddleware.cs`

```csharp
// Development vs Production error handling (lines 118-130)
if (_environment.IsDevelopment())
{
    response = new {
        StatusCode = context.Response.StatusCode,
        Message = exception.Message,
        Detail = exception.StackTrace
    };
}
else
{
    response = new {
        StatusCode = context.Response.StatusCode,
        Message = "An error occurred. Please try again later."
    };
}
```

---

## Frontend Security Implementation

### 1. Authentication Context

**File:** `pureledger-frontend/src/contexts/AuthContext.jsx`

**Token Storage (Current Implementation):**
```javascript
// Line 284 - Login success
localStorage.setItem('authToken', token);
localStorage.setItem('user', JSON.stringify(userData));

// Line 103 - Token retrieval
const token = localStorage.getItem('authToken');

// Line 314 - Logout
localStorage.removeItem('authToken');
localStorage.removeItem('user');
```

**Permission Checking:**
```javascript
// Lines 68-72
const hasPermission = (permission) => {
    if (!user) return false;
    if (user.isAdmin) return true;
    return permissions.includes(permission);
};
```

---

### 2. API Request Handling

**File:** `pureledger-frontend/src/services/api.js`

**Axios Interceptor - Adding Auth Token (lines 24-41):**
```javascript
apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('authToken');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);
```

**Token Expiry Handling (lines 64-78):**
```javascript
// Response interceptor
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const statusCode = error.response?.status;
        const originalRequest = error.config;

        if (statusCode === 401 && !originalRequest._retry) {
            originalRequest._retry = true;

            // Clear auth data and redirect to login
            localStorage.removeItem('authToken');
            localStorage.removeItem('user');

            if (window.location.pathname !== '/login') {
                window.location.href = '/login';
            }
        }
        return Promise.reject(error);
    }
);
```

---

### 3. Route Protection

**Implementation in AuthContext.jsx:**
```javascript
// Navigation filtered by permissions (lines 218-229)
const filteredNavItems = navigationItems.filter(item => {
    if (item.adminOnly && !user?.isAdmin) return false;
    if (item.permission && !hasPermission(item.permission)) return false;
    return true;
});
```

---

## Critical Vulnerabilities

### 1. Hardcoded JWT Secret Key

**Location:** `JwtService.cs:22,54` and `appsettings.json:18`

**Risk Level:** CRITICAL

**Current Code:**
```csharp
var secretKey = _configuration["Jwt:SecretKey"] ??
    "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
```

**Impact:**
- Anyone with access to source code can forge valid JWT tokens
- Complete authentication bypass possible

**Fix:**
```csharp
// Use Azure Key Vault, AWS Secrets Manager, or environment variables
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT secret key not configured");
```

---

### 2. Database Credentials in Configuration

**Location:** `appsettings.json:10`

**Risk Level:** CRITICAL

**Current:**
```json
"LedgerDb": "Host=localhost;Port=5432;Database=pureledgerdbtest;Username=postgres;Password=1324;"
```

**Impact:**
- Direct database access if configuration file is exposed
- Credentials visible in source control

**Fix:**
```json
// Use environment-specific configuration
"LedgerDb": "${DATABASE_CONNECTION_STRING}"
```

---

### 3. Tokens Stored in localStorage

**Location:** `AuthContext.jsx:103,284,314`

**Risk Level:** CRITICAL

**Current Code:**
```javascript
localStorage.setItem('authToken', token);
```

**Impact:**
- Vulnerable to XSS attacks - any injected JavaScript can steal tokens
- Tokens persist beyond browser session

**Fix Option 1 - HttpOnly Cookies:**
```csharp
// Backend - Set cookie instead of returning token
Response.Cookies.Append("authToken", token, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTime.UtcNow.AddDays(1)
});
```

**Fix Option 2 - sessionStorage:**
```javascript
// Less persistent than localStorage
sessionStorage.setItem('authToken', token);
```

---

### 4. Production Server IP Exposed

**Locations:**
- `appsettings.json:32-33`
- `pureledger-frontend/.env:1`

**Risk Level:** HIGH

**Impact:**
- Server address visible in source control
- Enables targeted attacks

**Fix:**
- Add production configuration to `.gitignore`
- Use CI/CD environment variables

---

## Security Issues by Priority

### Critical (Fix Before Production)

| Issue | Location | Risk |
|-------|----------|------|
| Hardcoded JWT Secret | `JwtService.cs:22` | Token forgery |
| Database Credentials Exposed | `appsettings.json:10` | Data breach |
| localStorage Token Storage | `AuthContext.jsx:284` | XSS token theft |
| Production IP in Source | Multiple files | Targeted attacks |

### High Priority

| Issue | Location | Risk |
|-------|----------|------|
| No Token Refresh Mechanism | `api.js:64-78` | Poor UX, forced re-login |
| Missing Project Access Validation | `AdminApiController.cs:542-578` | Data exposure |
| Overly Permissive CORS | `Program.cs:79-146` | Cross-origin attacks |

### Moderate Priority

| Issue | Location | Risk |
|-------|----------|------|
| Broad GET Permission | `ControllerAccessAttribute.cs:212-215` | Unintended data access |
| No Session Timeout | `AuthContext.jsx` | Extended exposure |
| No CSRF Protection | Backend & Frontend | Cross-site request forgery |
| User Data in localStorage | `AuthContext.jsx:285` | Information exposure |

---

## Positive Security Findings

| Feature | Implementation | Location |
|---------|---------------|----------|
| Password Hashing | BCrypt (strong, adaptive) | `AuthService.cs:63,108` |
| JWT Validation | All parameters validated | `Program.cs:148-171` |
| Global Auth Filter | Requires authentication | `Program.cs:200-207` |
| Role-Based Access | Detailed permission mapping | `ControllerAccessAttribute.cs` |
| Admin-Only Endpoints | Properly protected | `AdminApiController.cs:13` |
| Specific CORS Origins | Not using wildcard (*) | `appsettings.json` |
| Error Handling | Dev/Prod separation | `GlobalExceptionHandlerMiddleware.cs` |
| Bearer Token Format | Correctly implemented | `api.js:24-41` |
| SQL Injection Prevention | EF Core parameterized queries | Throughout |
| XSS Prevention | React auto-escapes | Frontend |

---

## Recommendations

### Immediate Actions (Before Production)

1. **Secure JWT Secret**
```bash
# Set environment variable
export JWT_SECRET_KEY="your-cryptographically-secure-key-here"
```

2. **Secure Database Credentials**
```bash
# Use User Secrets in development
dotnet user-secrets set "ConnectionStrings:LedgerDb" "your-connection-string"
```

3. **Switch Token Storage**
```javascript
// Option 1: sessionStorage (temporary)
sessionStorage.setItem('authToken', token);

// Option 2: HttpOnly cookie (preferred - backend change required)
```

4. **Update .gitignore**
```gitignore
# Environment files
.env.production
appsettings.Production.json
```

### High Priority (Next Sprint)

5. **Implement Refresh Tokens**
```csharp
// Add refresh token endpoint
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
{
    var principal = GetPrincipalFromExpiredToken(request.AccessToken);
    var refreshToken = await ValidateRefreshToken(request.RefreshToken);

    var newAccessToken = GenerateAccessToken(principal);
    var newRefreshToken = GenerateRefreshToken();

    return Ok(new { accessToken = newAccessToken, refreshToken = newRefreshToken });
}
```

6. **Add Session Timeout**
```javascript
// Add inactivity logout
let inactivityTimer;
const TIMEOUT_MINUTES = 30;

const resetTimer = () => {
    clearTimeout(inactivityTimer);
    inactivityTimer = setTimeout(logout, TIMEOUT_MINUTES * 60 * 1000);
};

document.addEventListener('mousemove', resetTimer);
document.addEventListener('keypress', resetTimer);
```

7. **Restrict Production CORS**
```json
"Cors": {
    "AllowedOrigins": [
        "https://pureledger.yourdomain.com"
    ]
}
```

### Medium Priority

8. **Add CSRF Protection**
```csharp
// Program.cs
builder.Services.AddAntiforgery(options => {
    options.HeaderName = "X-CSRF-TOKEN";
});
```

9. **Add Rate Limiting**
```csharp
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("api", config => {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
    });
});
```

10. **Add Security Headers**
```csharp
app.Use(async (context, next) => {
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    await next();
});
```

---

## Key Files Reference

### Backend Security Files

| File | Purpose |
|------|---------|
| `Services/JwtService.cs` | JWT token generation and validation |
| `Services/AuthService.cs` | User authentication, password handling |
| `Attributes/ControllerAccessAttribute.cs` | Authorization and permission checks |
| `Controllers/Api/AccountApiController.cs` | Login/logout endpoints |
| `Controllers/Api/AdminApiController.cs` | Admin-only operations |
| `Middleware/GlobalExceptionHandlerMiddleware.cs` | Error handling |
| `Program.cs` | Authentication/authorization configuration |
| `appsettings.json` | Configuration (JWT, CORS, DB) |

### Frontend Security Files

| File | Purpose |
|------|---------|
| `contexts/AuthContext.jsx` | Authentication state, token storage, permissions |
| `services/api.js` | API client, token injection, error handling |
| `.env` / `.env.development` / `.env.production` | Environment configuration |

---

## Missing Security Features

| Feature | Status | Impact |
|---------|--------|--------|
| Refresh Tokens | Not Implemented | Poor UX on token expiry |
| Session Timeout | Not Implemented | Extended exposure risk |
| CSRF Protection | Not Implemented | Cross-site request vulnerability |
| Rate Limiting | Not Implemented | DoS vulnerability |
| Audit Logging | Partial | Difficult incident investigation |
| Two-Factor Auth | Not Implemented | Single point of failure |
| Password Policy | Basic | Weak password risk |
| Account Lockout | Not Implemented | Brute force vulnerability |
| Security Headers | Not Implemented | Various browser-based attacks |
| Content Security Policy | Not Implemented | XSS risk |

---

*Document generated: December 2024*
*Analysis covers: PureLedger Frontend & Backend*
