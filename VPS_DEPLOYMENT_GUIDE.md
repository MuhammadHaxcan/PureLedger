# PureLedger VPS Deployment Guide

## Complete End-to-End Hosting Documentation

**Application:** PureLedger - Accounting/Ledger Management System
**Stack:** React (Vite) Frontend + .NET 8 Backend + PostgreSQL
**Domain:** tradeledger.cloud
**Last Updated:** December 2024

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [VPS Initial Setup](#2-vps-initial-setup)
3. [Install Required Software](#3-install-required-software)
4. [PostgreSQL Database Setup](#4-postgresql-database-setup)
5. [Backend Deployment](#5-backend-deployment)
6. [Frontend Deployment](#6-frontend-deployment)
7. [Nginx Configuration](#7-nginx-configuration)
8. [SSL/HTTPS with Let's Encrypt](#8-sslhttps-with-lets-encrypt)
9. [Systemd Service Configuration](#9-systemd-service-configuration)
10. [Security Configuration](#10-security-configuration)
11. [XSRF/CSRF Protection](#11-xsrfcsrf-protection)
12. [Environment Configuration](#12-environment-configuration)
13. [Deployment Commands Reference](#13-deployment-commands-reference)
14. [Troubleshooting](#14-troubleshooting)
15. [Maintenance & Updates](#15-maintenance--updates)
16. [Backup & Recovery](#16-backup--recovery)

---

## 1. Prerequisites

### 1.1 VPS Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| CPU | 1 vCPU | 2+ vCPU |
| RAM | 1 GB | 2+ GB |
| Storage | 20 GB SSD | 40+ GB SSD |
| OS | Ubuntu 22.04 LTS | Ubuntu 22.04/24.04 LTS |
| Bandwidth | 1 TB/month | Unlimited |

### 1.2 Domain Requirements

- Domain name pointed to VPS IP address
- DNS A records configured:
  ```
  tradeledger.cloud    -> VPS_IP_ADDRESS
  www.tradeledger.cloud -> VPS_IP_ADDRESS
  ```

### 1.3 Local Development Requirements

- Node.js 18+ (for frontend build)
- .NET 8 SDK (for backend build)
- Git

---

## 2. VPS Initial Setup

### 2.1 Connect to VPS

```bash
ssh root@YOUR_VPS_IP
```

### 2.2 Update System

```bash
apt update && apt upgrade -y
```

### 2.3 Create Non-Root User (Optional but Recommended)

```bash
adduser pureledger
usermod -aG sudo pureledger
```

### 2.4 Configure Firewall

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
ufw status
```

### 2.5 Set Timezone

```bash
timedatectl set-timezone UTC
# Or your preferred timezone:
# timedatectl set-timezone Asia/Karachi
```

---

## 3. Install Required Software

### 3.1 Install Nginx

```bash
apt install nginx -y
systemctl start nginx
systemctl enable nginx
systemctl status nginx
```

### 3.2 Install .NET 8 Runtime

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 8 Runtime (ASP.NET Core)
apt update
apt install -y aspnetcore-runtime-8.0

# Verify installation
dotnet --info
```

### 3.3 Install PostgreSQL

```bash
apt install postgresql postgresql-contrib -y
systemctl start postgresql
systemctl enable postgresql
systemctl status postgresql
```

### 3.4 Install Node.js (for building frontend)

```bash
# Install Node.js 20 LTS
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
apt install -y nodejs

# Verify
node --version
npm --version
```

### 3.5 Install Certbot (for SSL)

```bash
apt install certbot python3-certbot-nginx -y
```

---

## 4. PostgreSQL Database Setup

### 4.1 Access PostgreSQL

```bash
sudo -u postgres psql
```

### 4.2 Create Database and User

```sql
-- Create database
CREATE DATABASE pureledgerdb;

-- Create user with password
CREATE USER pureledgeruser WITH ENCRYPTED PASSWORD 'YOUR_SECURE_PASSWORD';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE pureledgerdb TO pureledgeruser;

-- Connect to the database
\c pureledgerdb

-- Grant schema privileges (required for .NET EF Core)
GRANT ALL ON SCHEMA public TO pureledgeruser;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO pureledgeruser;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO pureledgeruser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO pureledgeruser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO pureledgeruser;

-- Exit
\q
```

### 4.3 Configure PostgreSQL Authentication (Local Only)

```bash
# Edit pg_hba.conf
nano /etc/postgresql/14/main/pg_hba.conf
```

Ensure this line exists for local connections:
```
local   all             pureledgeruser                          md5
host    all             pureledgeruser  127.0.0.1/32            md5
```

```bash
# Restart PostgreSQL
systemctl restart postgresql
```

### 4.4 Test Connection

```bash
psql -U pureledgeruser -d pureledgerdb -h localhost
# Enter password when prompted
\q
```

### 4.5 Enable Remote Access to PostgreSQL (Optional)

> **WARNING:** Exposing PostgreSQL to the internet is a security risk. Only do this if you need external database access (e.g., for remote administration, external tools like pgAdmin, or connecting from another server). Always use strong passwords and consider using SSH tunneling instead.

#### Step 1: Configure PostgreSQL to Listen on All Interfaces

```bash
# Edit postgresql.conf
nano /etc/postgresql/14/main/postgresql.conf
```

Find and change:
```conf
# Before (default - local only)
#listen_addresses = 'localhost'

# After (listen on all interfaces)
listen_addresses = '*'
```

Also, you can optionally change the port (default is 5432):
```conf
port = 5432
```

#### Step 2: Allow Remote Connections in pg_hba.conf

```bash
# Edit pg_hba.conf
nano /etc/postgresql/14/main/pg_hba.conf
```

Add these lines at the end:

**Option A: Allow from ANY IP (less secure)**
```conf
# Allow connections from any IP (use strong password!)
host    all             pureledgeruser  0.0.0.0/0               md5
host    all             pureledgeruser  ::/0                    md5
```

**Option B: Allow from SPECIFIC IP only (more secure)**
```conf
# Allow connections from specific IP only
host    all             pureledgeruser  YOUR_CLIENT_IP/32       md5

# Example: Allow from 192.168.1.100
host    all             pureledgeruser  192.168.1.100/32        md5

# Example: Allow from IP range 10.0.0.0 - 10.0.0.255
host    all             pureledgeruser  10.0.0.0/24             md5
```

#### Step 3: Open Firewall Port

```bash
# Allow PostgreSQL port through firewall
ufw allow 5432/tcp

# Or restrict to specific IP
ufw allow from YOUR_CLIENT_IP to any port 5432

# Verify
ufw status
```

#### Step 4: Restart PostgreSQL

```bash
systemctl restart postgresql

# Verify it's listening on all interfaces
ss -tlnp | grep 5432
# Should show: 0.0.0.0:5432 (not just 127.0.0.1:5432)
```

#### Step 5: Test Remote Connection

From your local machine:
```bash
psql -h tradeledger.cloud -U pureledgeruser -d pureledgerdb -p 5432
# Enter password when prompted
```

Or using connection string:
```
Host=tradeledger.cloud;Port=5432;Database=pureledgerdb;Username=pureledgeruser;Password=YOUR_PASSWORD
```

#### Security Best Practices for Remote PostgreSQL

1. **Use Strong Passwords**: At least 20+ characters with mixed case, numbers, symbols
2. **Restrict IPs**: Only allow specific IPs if possible (use Option B above)
3. **Use SSL/TLS**: Configure PostgreSQL to require SSL connections
4. **Consider SSH Tunneling**: More secure than direct exposure
5. **Monitor Logs**: Check `/var/log/postgresql/` for suspicious activity
6. **Regular Updates**: Keep PostgreSQL updated for security patches

#### Alternative: SSH Tunnel (Recommended for Security)

Instead of exposing PostgreSQL directly, use SSH tunneling:

```bash
# From your local machine, create SSH tunnel
ssh -L 5432:localhost:5432 root@tradeledger.cloud -N

# Then connect to localhost:5432 (tunneled to VPS)
psql -h localhost -U pureledgeruser -d pureledgerdb -p 5432
```

This keeps PostgreSQL bound to localhost on the VPS while still allowing remote access through the encrypted SSH tunnel.

---

## 5. Backend Deployment

### 5.1 Create Directory Structure

```bash
mkdir -p /var/www/pureledger/backend-publish
mkdir -p /var/www/pureledger/backend-source
mkdir -p /var/www/pureledger/frontend-build
chown -R www-data:www-data /var/www/pureledger
```

### 5.2 Transfer Source Code

**Option A: Using Git**
```bash
cd /var/www/pureledger/backend-source
git clone YOUR_REPO_URL .
```

**Option B: Using SCP (from local machine)**
```bash
# From your local machine
scp -r ./Pureledger-Backend root@YOUR_VPS_IP:/var/www/pureledger/backend-source/
scp -r ./pureledger-frontend root@YOUR_VPS_IP:/var/www/pureledger/backend-source/
```

### 5.3 Configure appsettings.json for Production

Create/edit `/var/www/pureledger/backend-source/Pureledger-Backend/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "*:/api/AccountApi/login",
        "Period": "1m",
        "Limit": 5
      },
      {
        "Endpoint": "*:/api/AccountApi/refresh-token",
        "Period": "1m",
        "Limit": 10
      },
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  },
  "IpRateLimitPolicies": {
    "IpRules": []
  },
  "ConnectionStrings": {
    "LedgerDb": "Host=localhost;Port=5432;Database=pureledgerdb;Username=pureledgeruser;Password=YOUR_DB_PASSWORD;Pooling=true;"
  },
  "UploadSettings": {
    "ProjectImagesPath": "uploads/projects"
  },
  "Jwt": {
    "SecretKey": "YOUR_VERY_LONG_RANDOM_SECRET_KEY_AT_LEAST_64_CHARACTERS_LONG_FOR_PRODUCTION",
    "Issuer": "PureLedger",
    "Audience": "PureLedger",
    "ExpiryMinutes": "30",
    "RefreshTokenExpiryDays": "7"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://tradeledger.cloud",
      "https://tradeledger.cloud",
      "http://www.tradeledger.cloud",
      "https://www.tradeledger.cloud"
    ],
    "AllowCredentials": true,
    "AllowedHeaders": [ "*", "X-XSRF-TOKEN" ],
    "AllowedMethods": [ "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS" ],
    "PreflightMaxAge": 3600
  },
  "Security": {
    "CookieSettings": {
      "AccessTokenCookieName": "accessToken",
      "RefreshTokenCookieName": "refreshToken",
      "SecurePolicy": "SameAsRequest",
      "SameSiteMode": "Lax",
      "AccessTokenExpiryMinutes": 30,
      "RefreshTokenExpiryDays": 7
    }
  }
}
```

**Important Security Notes:**
- Generate a strong JWT SecretKey: `openssl rand -base64 64`
- Use a strong database password
- Never commit production secrets to git

### 5.4 Build Backend

**Option A: Build on VPS (requires .NET SDK)**
```bash
# Install .NET SDK if not installed
apt install -y dotnet-sdk-8.0

cd /var/www/pureledger/backend-source/Pureledger-Backend
dotnet publish -c Release -o /var/www/pureledger/backend-publish
```

**Option B: Build locally and transfer**
```bash
# On local machine
cd Pureledger-Backend
dotnet publish -c Release -o ./publish

# Transfer to VPS
scp -r ./publish/* root@YOUR_VPS_IP:/var/www/pureledger/backend-publish/
```

### 5.5 Set Permissions

```bash
chown -R www-data:www-data /var/www/pureledger/backend-publish
chmod -R 755 /var/www/pureledger/backend-publish
```

### 5.6 Run Database Migrations

```bash
cd /var/www/pureledger/backend-source/Pureledger-Backend

# If using EF Core migrations
dotnet ef database update

# Or run the published app once to auto-migrate (if configured)
```

---

## 6. Frontend Deployment

### 6.1 Configure Environment

Create `/var/www/pureledger/backend-source/pureledger-frontend/.env.production`:

```env
VITE_API_BASE_URL=/api
```

### 6.2 Build Frontend

```bash
cd /var/www/pureledger/backend-source/pureledger-frontend

# Install dependencies
npm install

# Build for production
npm run build

# Copy to serve directory
cp -r dist/* /var/www/pureledger/frontend-build/
```

### 6.3 Set Permissions

```bash
chown -R www-data:www-data /var/www/pureledger/frontend-build
chmod -R 755 /var/www/pureledger/frontend-build
```

---

## 7. Nginx Configuration

### 7.1 HTTP Only Configuration (Initial Setup)

Create `/etc/nginx/sites-available/pureledger.conf`:

```nginx
# Redirect www to non-www
server {
    listen 80;
    server_name www.tradeledger.cloud;
    return 301 http://tradeledger.cloud$request_uri;
}

server {
    listen 80;
    server_name tradeledger.cloud;

    # Frontend - React SPA
    location / {
        root /var/www/pureledger/frontend-build;
        index index.html;
        try_files $uri $uri/ /index.html;

        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # Backend API
    location /api/ {
        proxy_pass http://localhost:5104;
        proxy_http_version 1.1;

        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Connection keep-alive;

        proxy_pass_request_headers on;
        proxy_cookie_path /api /api;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
    }

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/json application/xml;
}
```

### 7.2 HTTPS Configuration (After SSL Setup)

```nginx
# Redirect HTTP to HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name tradeledger.cloud www.tradeledger.cloud;

    # Allow ACME challenge for Let's Encrypt certificate renewal
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }

    # Redirect all other HTTP requests to HTTPS
    location / {
        return 301 https://tradeledger.cloud$request_uri;
    }
}

# Redirect www to non-www (HTTPS)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name www.tradeledger.cloud;

    ssl_certificate /etc/letsencrypt/live/tradeledger.cloud/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/tradeledger.cloud/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    return 301 https://tradeledger.cloud$request_uri;
}

# Main HTTPS server
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name tradeledger.cloud;

    # SSL certificates (managed by Certbot)
    ssl_certificate /etc/letsencrypt/live/tradeledger.cloud/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/tradeledger.cloud/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header X-XSS-Protection "0" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Frontend - React SPA
    location / {
        root /var/www/pureledger/frontend-build;
        index index.html;
        try_files $uri $uri/ /index.html;

        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # Backend API
    location /api/ {
        proxy_pass http://localhost:5104;
        proxy_http_version 1.1;

        # Headers - IMPORTANT: X-Forwarded-Proto tells backend it's HTTPS
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Connection keep-alive;

        proxy_pass_request_headers on;
        proxy_cookie_path /api /api;

        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        proxy_buffering on;
        proxy_buffer_size 4k;
        proxy_buffers 8 4k;
    }

    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/json application/xml;
}
```

### 7.3 Enable Site Configuration

```bash
# Remove default site
rm /etc/nginx/sites-enabled/default

# Enable pureledger site
ln -s /etc/nginx/sites-available/pureledger.conf /etc/nginx/sites-enabled/

# Test configuration
nginx -t

# Reload nginx
systemctl reload nginx
```

---

## 8. SSL/HTTPS with Let's Encrypt

### 8.1 Obtain Certificate

```bash
# Using nginx plugin (recommended)
certbot --nginx -d tradeledger.cloud -d www.tradeledger.cloud

# Or using standalone (stop nginx first)
systemctl stop nginx
certbot certonly --standalone -d tradeledger.cloud -d www.tradeledger.cloud
systemctl start nginx
```

### 8.2 Verify Certificate

```bash
certbot certificates
```

### 8.3 Configure Auto-Renewal

```bash
# Edit renewal configuration
nano /etc/letsencrypt/renewal/tradeledger.cloud.conf
```

Ensure it has:
```
authenticator = nginx
```

```bash
# Test renewal
certbot renew --dry-run

# Certbot automatically creates a cron job/systemd timer for renewal
systemctl status certbot.timer
```

### 8.4 Manual Renewal (if needed)

```bash
certbot renew --nginx
```

---

## 9. Systemd Service Configuration

### 9.1 Create Service File

Create `/etc/systemd/system/pureledger-backend.service`:

```ini
[Unit]
Description=PureLedger Backend
After=network.target postgresql.service

[Service]
Type=simple
WorkingDirectory=/var/www/pureledger/backend-publish
ExecStart=/usr/bin/dotnet /var/www/pureledger/backend-publish/pureledger-backend.dll
Restart=always
RestartSec=10
User=www-data
Group=www-data

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5104
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=pureledger-backend

[Install]
WantedBy=multi-user.target
```

### 9.2 Enable and Start Service

```bash
# Reload systemd
systemctl daemon-reload

# Enable service (start on boot)
systemctl enable pureledger-backend

# Start service
systemctl start pureledger-backend

# Check status
systemctl status pureledger-backend

# View logs
journalctl -u pureledger-backend -f
```

### 9.3 Service Management Commands

```bash
# Start
systemctl start pureledger-backend

# Stop
systemctl stop pureledger-backend

# Restart
systemctl restart pureledger-backend

# Reload (if supported)
systemctl reload pureledger-backend

# View recent logs
journalctl -u pureledger-backend -n 100

# View logs since boot
journalctl -u pureledger-backend -b

# Follow logs in real-time
journalctl -u pureledger-backend -f
```

---

## 10. Security Configuration

### 10.1 Cookie Security

The application uses the following cookie configuration:

| Cookie | HttpOnly | Secure | SameSite | Path |
|--------|----------|--------|----------|------|
| accessToken | Yes | Auto* | Lax | /api |
| refreshToken | Yes | Auto* | Lax | /api |
| XSRF-TOKEN | No | Auto* | Lax | / |

*Auto = `true` for HTTPS, `false` for HTTP (detected via X-Forwarded-Proto)

### 10.2 Backend Security Features

1. **JWT Authentication** - Tokens stored in HttpOnly cookies
2. **XSRF Protection** - Double-submit cookie pattern
3. **Rate Limiting** - Prevents brute force attacks
4. **CORS** - Restricts cross-origin requests
5. **Security Headers** - Added via middleware and nginx

### 10.3 Nginx Security Headers

```nginx
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header X-XSS-Protection "0" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
```

### 10.4 Firewall Rules

```bash
# View current rules
ufw status verbose

# Only allow necessary ports
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
```

### 10.5 PostgreSQL Security

#### Default (Local Only - Recommended)
```bash
# Edit PostgreSQL configuration
nano /etc/postgresql/14/main/postgresql.conf
```

Ensure:
```conf
listen_addresses = 'localhost'  # Only allow local connections
```

#### If Remote Access is Enabled

If you've enabled remote PostgreSQL access (see Section 4.5), implement these security measures:

```bash
# 1. Check current connections
sudo -u postgres psql -c "SELECT * FROM pg_stat_activity;"

# 2. Monitor failed login attempts
tail -f /var/log/postgresql/postgresql-14-main.log | grep "FATAL"

# 3. List firewall rules for PostgreSQL
ufw status | grep 5432
```

**Security Checklist for Remote PostgreSQL:**

| Check | Command/Action |
|-------|----------------|
| Strong password | At least 20+ chars, mixed case, numbers, symbols |
| IP restriction | Use specific IPs in pg_hba.conf, not 0.0.0.0/0 |
| Firewall | `ufw allow from SPECIFIC_IP to any port 5432` |
| SSL enabled | Configure `ssl = on` in postgresql.conf |
| Monitor logs | Check `/var/log/postgresql/` regularly |
| Updates | `apt update && apt upgrade postgresql` |

**To disable remote access (revert to local only):**
```bash
# 1. Edit postgresql.conf
nano /etc/postgresql/14/main/postgresql.conf
# Change: listen_addresses = 'localhost'

# 2. Remove remote entries from pg_hba.conf
nano /etc/postgresql/14/main/pg_hba.conf
# Remove lines with 0.0.0.0/0 or external IPs

# 3. Close firewall port
ufw delete allow 5432/tcp

# 4. Restart PostgreSQL
systemctl restart postgresql
```

---

## 11. XSRF/CSRF Protection

### 11.1 How It Works

PureLedger uses the **Double-Submit Cookie Pattern**:

1. **Login**: Backend generates random token, sets it in cookie AND returns in response
2. **Frontend**: Reads token from cookie, sends in `X-XSRF-TOKEN` header
3. **Backend**: Compares cookie value with header value (must match)

### 11.2 Implementation Details

**Backend (ValidateXsrfTokenAttribute.cs)**:
- Validates on POST, PUT, DELETE, PATCH requests
- Compares `XSRF-TOKEN` cookie with `X-XSRF-TOKEN` header
- Auto-detects HTTPS for Secure flag

**Frontend (api.js)**:
- Reads token from cookie: `document.cookie.match(/XSRF-TOKEN=([^;]*)/)`
- Sends in header for state-changing requests
- Auto-retries on token mismatch

### 11.3 Token Flow Diagram

```
┌─────────────┐                    ┌─────────────┐
│   Browser   │                    │   Backend   │
└─────────────┘                    └─────────────┘
       │                                  │
       │  POST /api/login                 │
       │─────────────────────────────────>│
       │                                  │
       │  Set-Cookie: XSRF-TOKEN=abc123   │
       │  Body: { xsrfToken: "abc123" }   │
       │<─────────────────────────────────│
       │                                  │
       │  POST /api/data                  │
       │  Cookie: XSRF-TOKEN=abc123       │
       │  Header: X-XSRF-TOKEN=abc123     │
       │─────────────────────────────────>│
       │                                  │
       │  (Backend compares values)       │
       │  200 OK                          │
       │<─────────────────────────────────│
```

---

## 12. Environment Configuration

### 12.1 Frontend Environment Files

**.env.development** (local development):
```env
VITE_API_BASE_URL=http://localhost:5221/api
```

**.env.production** (VPS deployment):
```env
VITE_API_BASE_URL=/api
```

### 12.2 Backend Environment Variables

Set in systemd service or shell:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5104
DOTNET_PRINT_TELEMETRY_MESSAGE=false
```

### 12.3 Generate Secure Keys

```bash
# Generate JWT Secret Key
openssl rand -base64 64

# Generate Database Password
openssl rand -base64 32
```

---

## 13. Deployment Commands Reference

### 13.1 Full Deployment Script

```bash
#!/bin/bash
# deploy.sh - Full deployment script

set -e  # Exit on error

echo "=== Deploying PureLedger ==="

# Variables
BACKEND_SOURCE="/var/www/pureledger/backend-source/Pureledger-Backend"
BACKEND_PUBLISH="/var/www/pureledger/backend-publish"
FRONTEND_SOURCE="/var/www/pureledger/backend-source/pureledger-frontend"
FRONTEND_PUBLISH="/var/www/pureledger/frontend-build"

# 1. Pull latest code (if using git)
echo "=== Pulling latest code ==="
cd /var/www/pureledger/backend-source
git pull origin main

# 2. Build and deploy backend
echo "=== Building backend ==="
cd $BACKEND_SOURCE
dotnet publish -c Release -o $BACKEND_PUBLISH

# 3. Build and deploy frontend
echo "=== Building frontend ==="
cd $FRONTEND_SOURCE
npm install
npm run build
rm -rf $FRONTEND_PUBLISH/*
cp -r dist/* $FRONTEND_PUBLISH/

# 4. Set permissions
echo "=== Setting permissions ==="
chown -R www-data:www-data /var/www/pureledger

# 5. Restart backend
echo "=== Restarting backend ==="
systemctl restart pureledger-backend

# 6. Reload nginx (if config changed)
echo "=== Reloading nginx ==="
nginx -t && systemctl reload nginx

echo "=== Deployment complete ==="
systemctl status pureledger-backend --no-pager
```

### 13.2 Quick Commands

```bash
# Backend only
cd /var/www/pureledger/backend-source/Pureledger-Backend && \
dotnet publish -c Release -o /var/www/pureledger/backend-publish && \
systemctl restart pureledger-backend

# Frontend only
cd /var/www/pureledger/backend-source/pureledger-frontend && \
npm run build && \
cp -r dist/* /var/www/pureledger/frontend-build/

# Check all services
systemctl status nginx pureledger-backend postgresql

# View all logs
journalctl -u pureledger-backend -u nginx -f
```

---

## 14. Troubleshooting

### 14.1 Common Issues

#### Backend Not Starting

```bash
# Check logs
journalctl -u pureledger-backend -n 50

# Common causes:
# - Wrong database connection string
# - Port already in use
# - Missing permissions
# - Missing .NET runtime

# Test manually
cd /var/www/pureledger/backend-publish
dotnet pureledger-backend.dll
```

#### 502 Bad Gateway

```bash
# Check if backend is running
systemctl status pureledger-backend

# Check if listening on correct port
ss -tlnp | grep 5104

# Check nginx error logs
tail -f /var/log/nginx/error.log
```

#### CORS Errors

1. Check `AllowedOrigins` in appsettings.json includes your domain
2. Ensure both HTTP and HTTPS versions are listed
3. Check browser console for specific CORS error

#### XSRF Token Invalid

1. Clear all cookies for the domain
2. Check browser DevTools:
   - Application > Cookies: Is XSRF-TOKEN present?
   - Network > Request Headers: Is X-XSRF-TOKEN header sent?
   - Compare cookie and header values (must match)

3. Check if www vs non-www redirect is working

#### SSL Certificate Issues

```bash
# Check certificate status
certbot certificates

# Test renewal
certbot renew --dry-run

# Check nginx SSL config
nginx -t

# Check SSL certificate dates
openssl s_client -connect tradeledger.cloud:443 -servername tradeledger.cloud 2>/dev/null | openssl x509 -noout -dates
```

### 14.2 Log Locations

| Log | Location |
|-----|----------|
| Backend | `journalctl -u pureledger-backend` |
| Nginx Access | `/var/log/nginx/access.log` |
| Nginx Error | `/var/log/nginx/error.log` |
| PostgreSQL | `/var/log/postgresql/postgresql-14-main.log` |
| Certbot | `/var/log/letsencrypt/letsencrypt.log` |

### 14.3 Debug Mode

To run backend in debug mode temporarily:

```bash
systemctl stop pureledger-backend
cd /var/www/pureledger/backend-publish
ASPNETCORE_ENVIRONMENT=Development dotnet pureledger-backend.dll
```

---

## 15. Maintenance & Updates

### 15.1 Regular Maintenance Tasks

**Daily:**
- Monitor logs for errors: `journalctl -u pureledger-backend --since today`

**Weekly:**
- Check disk space: `df -h`
- Check system updates: `apt update && apt list --upgradable`

**Monthly:**
- Apply security updates: `apt upgrade -y`
- Review access logs
- Check SSL certificate expiry: `certbot certificates`
- Backup database

### 15.2 Updating the Application

```bash
# 1. Backup current version
cp -r /var/www/pureledger/backend-publish /var/www/pureledger/backend-publish.bak
cp -r /var/www/pureledger/frontend-build /var/www/pureledger/frontend-build.bak

# 2. Pull latest code
cd /var/www/pureledger/backend-source
git pull

# 3. Deploy (use deploy.sh script)
./deploy.sh

# 4. If something goes wrong, rollback
cp -r /var/www/pureledger/backend-publish.bak/* /var/www/pureledger/backend-publish/
cp -r /var/www/pureledger/frontend-build.bak/* /var/www/pureledger/frontend-build/
systemctl restart pureledger-backend
```

### 15.3 Database Migrations

```bash
# If you have new migrations
cd /var/www/pureledger/backend-source/Pureledger-Backend

# Apply migrations
dotnet ef database update --connection "Host=localhost;Database=pureledgerdb;Username=pureledgeruser;Password=YOUR_PASSWORD"
```

---

## 16. Backup & Recovery

This setup follows the same basic flow as the FreightForwarding V3 deployment:
create a compressed PostgreSQL dump on the VPS, then pull it to the Windows
laptop over SSH. Pureledger adds a small systemd service/timer and checksum
verification so both steps can run unattended.

**Backup schedule and retention:**

| Location | Schedule | Retention |
|----------|----------|-----------|
| VPS `/var/backups/pureledger` | Daily at 02:00 (VPS timezone) | 14 days |
| Laptop `C:\Users\Claude\Desktop\pureledger_backups` | Daily at 03:30 | 30 days |

> The commands below are intentionally split into **LOCAL POWERSHELL** and
> **VPS (SSH)** blocks. Run each block only on the named machine.

### 16.1 Files Included in the Repository

| File | Purpose |
|------|---------|
| `deployment/backup/pureledger-backup.sh` | Creates, validates, checksums, and expires VPS dumps |
| `deployment/backup/install-vps-backup.sh` | Installs the VPS backup user, script, service, and timer |
| `deployment/backup/pureledger-backup.service` | Runs one database backup |
| `deployment/backup/pureledger-backup.timer` | Starts the service daily at 02:00 |
| `deployment/backup/pull-pureledger-backups.ps1` | Pulls and verifies backups on Windows |
| `deployment/backup/install-windows-task.ps1` | Registers the daily 03:30 Windows task |

### 16.2 Step 1 - Generate a Dedicated SSH Key on the Laptop

Run in **LOCAL POWERSHELL**. The empty passphrase is required because Task
Scheduler cannot answer an interactive key prompt.

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\.ssh" | Out-Null

# Do not run this command if this key file already exists.
Test-Path "$env:USERPROFILE\.ssh\pureledger_backup_ed25519"

# Keep this command on one line. Multiline PowerShell backticks are easy to
# break when copied because the backtick must be the final character on a line.
ssh-keygen.exe -t ed25519 -C "pureledger-backup-laptop" -f "$env:USERPROFILE\.ssh\pureledger_backup_ed25519" -N '""'
```

If PowerShell shows `>>` instead of the normal `PS C:\...>` prompt, press
`Ctrl+C` once to cancel the incomplete command, then paste the single-line
command above.

Expected files:

```text
C:\Users\Claude\.ssh\pureledger_backup_ed25519
C:\Users\Claude\.ssh\pureledger_backup_ed25519.pub
```

Keep the private file private. Only the `.pub` file is copied to the VPS.

### 16.3 Step 2 - Copy the VPS Setup Files

Run in **LOCAL POWERSHELL** from the Pureledger repository:

```powershell
Set-Location "C:\Users\Claude\Desktop\Pureledger\PureLedger"

ssh root@72.60.209.26 "mkdir -p /tmp/pureledger-backup-setup"

# Keep this command on one line when copying it into Windows PowerShell.
scp .\deployment\backup\pureledger-backup.sh .\deployment\backup\install-vps-backup.sh .\deployment\backup\pureledger-backup.service .\deployment\backup\pureledger-backup.timer root@72.60.209.26:/tmp/pureledger-backup-setup/
```

Enter the existing VPS root password when SSH/SCP requests it.

### 16.4 Step 3 - Install the VPS Backup Service

First connect from **LOCAL POWERSHELL**:

```powershell
ssh root@72.60.209.26
```

Then run on **VPS (SSH)**:

```bash
cd /tmp/pureledger-backup-setup
chmod +x install-vps-backup.sh pureledger-backup.sh
./install-vps-backup.sh
```

The installer asks for the PostgreSQL password for `pureledgeruser`. It stores
the password only in `/root/.pgpass` with mode `0600`; the password is not placed
in the scripts, systemd service, command history, or this guide.

The installer creates:

- non-sudo SSH user `pureledger-backup`;
- backup directory `/var/backups/pureledger`;
- command `/usr/local/sbin/pureledger-backup`;
- systemd units `pureledger-backup.service` and `pureledger-backup.timer`.

Confirm the timer on **VPS (SSH)**:

```bash
systemctl status pureledger-backup.timer --no-pager
systemctl list-timers pureledger-backup.timer --all
```

### 16.5 Step 4 - Install the Laptop Public Key on the VPS

Exit the VPS shell first:

```bash
exit
```

Run in **LOCAL POWERSHELL**. This uses the root password one final time:

```powershell
Get-Content "$env:USERPROFILE\.ssh\pureledger_backup_ed25519.pub" | ssh root@72.60.209.26 "cat > /home/pureledger-backup/.ssh/authorized_keys && chown pureledger-backup:pureledger-backup /home/pureledger-backup/.ssh/authorized_keys && chmod 600 /home/pureledger-backup/.ssh/authorized_keys"
```

Verify passwordless access in **LOCAL POWERSHELL**:

```powershell
# Keep this command on one line when copying it into Windows PowerShell.
ssh -i "$env:USERPROFILE\.ssh\pureledger_backup_ed25519" -o IdentitiesOnly=yes pureledger-backup@72.60.209.26 "ls -la /var/backups/pureledger"
```

This command must not ask for the VPS password. Type `yes` only if SSH asks you
to trust the host fingerprint on the first connection.

### 16.6 Step 5 - Run and Verify the First VPS Backup

Connect and run these commands manually:

```powershell
# LOCAL POWERSHELL
ssh root@72.60.209.26
```

```bash
# VPS (SSH)
systemctl start pureledger-backup.service
systemctl status pureledger-backup.service --no-pager
journalctl -u pureledger-backup.service -n 50 --no-pager
ls -lh /var/backups/pureledger/

cd /var/backups/pureledger
LATEST_DUMP="$(ls -1t pureledgerdb_*.dump | head -n 1)"
pg_restore --list "$LATEST_DUMP" >/dev/null
sha256sum --check "$LATEST_DUMP.sha256"
```

Expected checksum result:

```text
pureledgerdb_YYYYMMDD_HHMMSS.dump: OK
```

### 16.7 Step 6 - Test the Laptop Pull Manually

Exit the VPS, then run in **LOCAL POWERSHELL**:

```powershell
Set-Location "C:\Users\Claude\Desktop\Pureledger\PureLedger"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\deployment\backup\pull-pureledger-backups.ps1"

Get-ChildItem "C:\Users\Claude\Desktop\pureledger_backups"
Get-Content "C:\Users\Claude\Desktop\pureledger_backups\logs\pureledger-backup-pull.log" -Tail 30
```

The pull script downloads only missing or invalid backup pairs. Files remain
temporary with a `.partial` suffix until their SHA-256 checksum passes.

### 16.8 Step 7 - Install the Windows 03:30 Scheduled Task

Run in **LOCAL POWERSHELL** as the Windows user who owns the SSH key:

```powershell
Set-Location "C:\Users\Claude\Desktop\Pureledger\PureLedger"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\deployment\backup\install-windows-task.ps1"
```

The task runs at 03:30, starts after a missed schedule, and refuses to start a
second copy while the first is running. It uses the current interactive Windows
user and stores no Windows password, so that user must be logged on when the task
runs.

Test and inspect the task in **LOCAL POWERSHELL**:

```powershell
Start-ScheduledTask -TaskName "Pureledger Backup Pull"
Start-Sleep -Seconds 10
Get-ScheduledTask -TaskName "Pureledger Backup Pull"
Get-ScheduledTaskInfo -TaskName "Pureledger Backup Pull"
Get-Content "C:\Users\Claude\Desktop\pureledger_backups\logs\pureledger-backup-pull.log" -Tail 30
```

`LastTaskResult` equal to `0` means the pull completed successfully.

### 16.9 Daily Status and Troubleshooting Commands

**VPS (SSH):**

```bash
systemctl status pureledger-backup.timer --no-pager
systemctl list-timers pureledger-backup.timer --all
journalctl -u pureledger-backup.service --since today --no-pager
ls -lh /var/backups/pureledger/
df -h /var/backups

# Run an additional backup immediately
systemctl start pureledger-backup.service

# Re-run the installer after changing repository service/script files
cd /tmp/pureledger-backup-setup
./install-vps-backup.sh
```

**LOCAL POWERSHELL:**

```powershell
Get-ScheduledTask -TaskName "Pureledger Backup Pull"
Get-ScheduledTaskInfo -TaskName "Pureledger Backup Pull"
Get-ChildItem "C:\Users\Claude\Desktop\pureledger_backups" -Filter "*.dump"
Get-Content "C:\Users\Claude\Desktop\pureledger_backups\logs\pureledger-backup-pull.log" -Tail 50

# Run a pull immediately without waiting until 03:30
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Claude\Desktop\Pureledger\PureLedger\deployment\backup\pull-pureledger-backups.ps1"
```

Common failures:

| Symptom | Check |
|---------|-------|
| VPS service reports authentication failure | Confirm `/root/.pgpass` is mode `0600` and contains the correct `pureledgeruser` password |
| Laptop asks for an SSH password | Re-run section 16.5 and confirm the private key path |
| `Permission denied` reading a dump | Confirm files and directory use group `pureledger-backup` |
| Windows task result is nonzero | Read `pureledger-backup-pull.log`, then run the pull command manually |
| No backup at exactly 02:00 | Check VPS timezone with `timedatectl` and inspect `systemctl list-timers` |

### 16.10 Restore a Backup Locally on Windows

Choose a real `.dump` filename from
`C:\Users\Claude\Desktop\pureledger_backups`. Adjust the PostgreSQL version in
`$pgBin` if needed.

```powershell
$pgBin = "C:\Program Files\PostgreSQL\18\bin"
$dump = "C:\Users\Claude\Desktop\pureledger_backups\pureledgerdb_YYYYMMDD_HHMMSS.dump"

# Create a separate local database so an existing database is not overwritten.
& "$pgBin\createdb.exe" -h localhost -p 5432 -U postgres pureledgerdb_local

& "$pgBin\pg_restore.exe" `
  -h localhost -p 5432 -U postgres `
  -d pureledgerdb_local `
  --no-owner --no-privileges --verbose `
  $dump
```

PostgreSQL prompts for the local `postgres` password. Point the local backend's
`ConnectionStrings:LedgerDb` at `pureledgerdb_local` only after the restore
finishes successfully.

### 16.11 Restore a Backup to the VPS (Destructive)

> **WARNING:** This replaces the live `pureledgerdb`. Confirm the selected dump
> and keep another verified backup before continuing.

If the selected dump exists only on the laptop, upload it in **LOCAL
POWERSHELL**:

```powershell
scp "C:\Users\Claude\Desktop\pureledger_backups\pureledgerdb_YYYYMMDD_HHMMSS.dump" `
    "C:\Users\Claude\Desktop\pureledger_backups\pureledgerdb_YYYYMMDD_HHMMSS.dump.sha256" `
    root@72.60.209.26:/tmp/

ssh root@72.60.209.26
```

Then run on **VPS (SSH)** after replacing the filename:

```bash
cd /tmp
sha256sum --check pureledgerdb_YYYYMMDD_HHMMSS.dump.sha256

systemctl stop pureledger-backend

sudo -u postgres psql -d postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'pureledgerdb' AND pid <> pg_backend_pid();"
sudo -u postgres dropdb --if-exists pureledgerdb
sudo -u postgres createdb --owner=pureledgeruser pureledgerdb

PGPASSFILE=/root/.pgpass pg_restore \
  -h localhost -p 5432 -U pureledgeruser \
  -d pureledgerdb --no-owner --no-privileges --verbose \
  pureledgerdb_YYYYMMDD_HHMMSS.dump

systemctl start pureledger-backend
systemctl status pureledger-backend --no-pager
```

After restore, open the application and verify representative records before
deleting the uploaded `/tmp` files.

### 16.12 What This Backup Does Not Include

The database dump does not include application source, Nginx configuration, or
Let's Encrypt certificates. Continue using Git for source code and VPS-provider
snapshots if a full-server disaster-recovery copy is required.

---

## Appendix A: File Structure

```
/var/www/pureledger/
├── backend-publish/           # Compiled backend (served by systemd)
│   ├── pureledger-backend.dll
│   ├── appsettings.json
│   └── ...
├── backend-source/            # Source code (for updates)
│   ├── Pureledger-Backend/
│   └── pureledger-frontend/
└── frontend-build/            # Compiled frontend (served by nginx)
    ├── index.html
    ├── assets/
    └── ...

/etc/nginx/sites-available/
└── pureledger.conf           # Nginx configuration

/etc/systemd/system/
└── pureledger-backend.service # Systemd service

/etc/letsencrypt/live/tradeledger.cloud/
├── fullchain.pem             # SSL certificate
├── privkey.pem               # SSL private key
└── ...
```

---

## Appendix B: Port Reference

| Service | Port | Protocol | Exposure |
|---------|------|----------|----------|
| Nginx (HTTP) | 80 | TCP | Public |
| Nginx (HTTPS) | 443 | TCP | Public |
| Backend (internal) | 5104 | TCP | Internal only (via nginx proxy) |
| PostgreSQL | 5432 | TCP | Internal by default, or Public if remote access enabled |
| SSH | 22 | TCP | Public |

**Firewall Rules Summary:**
```bash
# Minimum required (default)
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS

# Optional: If PostgreSQL remote access is needed
ufw allow 5432/tcp  # PostgreSQL (or restrict to specific IP)
```

---

## Appendix C: Useful Commands

```bash
# System
systemctl status nginx pureledger-backend postgresql
df -h                          # Disk space
free -m                        # Memory usage
htop                          # Process monitor

# Nginx
nginx -t                       # Test config
systemctl reload nginx         # Reload config
tail -f /var/log/nginx/*.log  # View logs

# Backend
systemctl restart pureledger-backend
journalctl -u pureledger-backend -f

# Database
sudo -u postgres psql         # Access PostgreSQL
psql -U pureledgeruser -d pureledgerdb -h localhost

# SSL
certbot certificates          # View certificates
certbot renew --dry-run       # Test renewal
certbot renew                 # Renew certificates

# Firewall
ufw status verbose
ufw allow 80/tcp
ufw allow 443/tcp
```

---

## Appendix D: Switching Git Branch on VPS

If the VPS is currently on a different branch (e.g., `BusinessLogicFixed`) and you need to switch to `master`:

### D.1 Check Current Branch

```bash
cd /var/www/pureledger/backend-source
git branch
# Shows current branch with asterisk (*)
```

### D.2 Switch to Master Branch

```bash
# Fetch latest from remote
git fetch origin

# Stash any local changes (if any)
git stash

# Switch to master branch
git checkout master

# Pull latest changes
git pull origin master
```

### D.3 Redeploy After Branch Switch

```bash
# Rebuild and deploy backend
cd /var/www/pureledger/backend-source/Pureledger-Backend
dotnet publish -c Release -o /var/www/pureledger/backend-publish

# Rebuild and deploy frontend
cd /var/www/pureledger/backend-source/pureledger-frontend
npm install
npm run build
cp -r dist/* /var/www/pureledger/frontend-build/

# Set permissions
chown -R www-data:www-data /var/www/pureledger

# Restart backend service
systemctl restart pureledger-backend

# Verify
systemctl status pureledger-backend
```

### D.4 Verify Branch Switch

```bash
cd /var/www/pureledger/backend-source
git branch
# Should show: * master
```

### D.5 If You Need to Go Back

```bash
# Switch back to previous branch
git checkout BusinessLogicFixed
git pull origin BusinessLogicFixed

# Then redeploy (repeat D.3 steps)
```

---

## Appendix E: Database Dump & Restore

The complete ordered setup and restore procedure is in [Section 16](#16-backup--recovery).

### E.1 Quick Reference Commands

| Task | Command |
|------|---------|
| Run VPS backup now | `systemctl start pureledger-backup.service` |
| View VPS backup logs | `journalctl -u pureledger-backup.service -n 50 --no-pager` |
| Show next automatic run | `systemctl list-timers pureledger-backup.timer --all` |
| List VPS dumps | `ls -lh /var/backups/pureledger/` |
| Run laptop pull now | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Claude\Desktop\Pureledger\PureLedger\deployment\backup\pull-pureledger-backups.ps1"` |
| Inspect Windows task | `Get-ScheduledTaskInfo -TaskName "Pureledger Backup Pull"` |
| List laptop dumps | `Get-ChildItem "C:\Users\Claude\Desktop\pureledger_backups" -Filter "*.dump"` |

### E.2 Backup File Naming

Each completed backup is a verified pair:

```text
pureledgerdb_YYYYMMDD_HHMMSS.dump
pureledgerdb_YYYYMMDD_HHMMSS.dump.sha256
```

Do not treat `.partial` files as valid backups. Never restore a dump unless its
matching `.sha256` file passes verification.

---

## Appendix F: Quick Deploy After Code Push

### F.1 Full Rebuild (Frontend + Backend)

```bash
# SSH into VPS
ssh root@72.60.209.26

# Pull latest code
cd /var/www/pureledger/backend-source && git pull origin master

# Build backend
cd /var/www/pureledger/backend-source/Pureledger-Backend && \
dotnet publish -c Release -o /var/www/pureledger/backend-publish

# Build frontend
cd /var/www/pureledger/backend-source/pureledger-frontend && \
npm install && npm run build && \
cp -r dist/* /var/www/pureledger/frontend-build/

# Restart backend service
systemctl restart pureledger-backend

# Verify
systemctl status pureledger-backend
```

### F.2 Backend Only

```bash
ssh root@72.60.209.26
cd /var/www/pureledger/backend-source && git pull origin master
cd Pureledger-Backend && dotnet publish -c Release -o /var/www/pureledger/backend-publish
systemctl restart pureledger-backend
systemctl status pureledger-backend
```

### F.3 Frontend Only

```bash
ssh root@72.60.209.26
cd /var/www/pureledger/backend-source && git pull origin master
cd pureledger-frontend && npm install && npm run build
cp -r dist/* /var/www/pureledger/frontend-build/
```

### F.4 One-Liner Commands (Copy-Paste Ready)

> **Note:** Must stop service before publishing to avoid file lock errors.

**Full deploy:**
```bash
cd /var/www/pureledger/backend-source && git pull origin master && systemctl stop pureledger-backend && cd Pureledger-Backend && dotnet publish -c Release -o /var/www/pureledger/backend-publish && cd ../pureledger-frontend && npm install && npm run build && cp -r dist/* /var/www/pureledger/frontend-build/ && systemctl start pureledger-backend && systemctl status pureledger-backend
```

**Backend only:**
```bash
cd /var/www/pureledger/backend-source && git pull origin master && systemctl stop pureledger-backend && cd Pureledger-Backend && dotnet publish -c Release -o /var/www/pureledger/backend-publish && systemctl start pureledger-backend && systemctl status pureledger-backend
```

**Frontend only:**
```bash
cd /var/www/pureledger/backend-source && git pull origin master && cd pureledger-frontend && npm install && npm run build && cp -r dist/* /var/www/pureledger/frontend-build/
```

---

**Document Version:** 1.4
**Created:** December 2024
**Updated:** July 2026
**Author:** PureLedger DevOps
