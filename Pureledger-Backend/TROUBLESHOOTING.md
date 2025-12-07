# Troubleshooting Guide

## Common Errors

### 500 Internal Server Error

If you're getting 500 errors, check:

1. **Backend Console Logs** - The error details will be logged there
2. **Database Connection** - Verify PostgreSQL is running and connection string is correct
3. **Configuration** - Ensure all required values are in `appsettings.json`

### 401 Unauthorized Errors

If you're getting 401 errors:

1. **Check Login Credentials** - Verify username and password are correct
2. **JWT Configuration** - Ensure JWT settings in `appsettings.json` are correct
3. **Token Expiry** - Your token might have expired, try logging in again

## Configuration Checklist

Ensure all these values are present in `appsettings.json`:

- ✅ `ConnectionStrings:LedgerDb` - Database connection string
- ✅ `Jwt:SecretKey` - JWT secret key
- ✅ `Jwt:Issuer` - JWT issuer
- ✅ `Jwt:Audience` - JWT audience
- ✅ `Cors:AllowedOrigins` - Array of allowed origins
- ✅ `Cors:AllowCredentials` - Boolean
- ✅ `Cors:AllowedHeaders` - Array of allowed headers
- ✅ `Cors:AllowedMethods` - Array of allowed HTTP methods
- ✅ `Cors:PreflightMaxAge` - Integer value

## Checking Backend Logs

To see detailed error information:

1. Open the backend console/terminal where you're running `dotnet run`
2. Look for error messages with stack traces
3. Check for database connection errors
4. Verify configuration values are being read correctly

## Restart Backend After Configuration Changes

After making changes to `appsettings.json` or `Program.cs`:
1. Stop the backend (Ctrl+C)
2. Rebuild: `dotnet build`
3. Restart: `dotnet run`

