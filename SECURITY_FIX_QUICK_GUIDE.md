# Quick Guide: Apply Security Fix for Token Invalidation

## Critical Security Fix
This fix ensures that when a user changes their password, ALL existing sessions (JWT tokens and refresh tokens) across all devices are immediately invalidated.

## What Was Changed

### Backend Changes
1. Added `SecurityStamp` field to User model
2. Created database migration to add SecurityStamp column
3. Updated JWT token generation to include SecurityStamp claim
4. Created middleware to validate SecurityStamp on each request
5. Updated password change logic to revoke all tokens

## Files Modified

### New Files Created
- `Pureledger-Backend/Migrations/20251214120000_AddSecurityStampToUser.cs`
- `Pureledger-Backend/Middleware/SecurityStampValidationMiddleware.cs`
- `SECURITY_FIX_IMPLEMENTATION.md` (documentation)
- `SECURITY_FIX_QUICK_GUIDE.md` (this file)

### Modified Files
- `Pureledger-Backend/Models/User.cs`
- `Pureledger-Backend/Migrations/LedgerDbContextModelSnapshot.cs`
- `Pureledger-Backend/Services/JwtService.cs`
- `Pureledger-Backend/Services/Interface/IJwtService.cs`
- `Pureledger-Backend/Services/AuthService.cs`
- `Pureledger-Backend/Controllers/Api/AccountApiController.cs`
- `Pureledger-Backend/Program.cs`

## Deployment Steps

### Step 1: Stop the Backend (if running)
```bash
# If running as a service
sudo systemctl stop pureledger-backend

# Or if running manually, press Ctrl+C
```

### Step 2: Apply Database Migration

**Option A: Using Entity Framework CLI**
```bash
cd Pureledger-Backend
dotnet ef database update
```

**Option B: Using Direct SQL (PostgreSQL)**
```sql
-- Connect to your database
psql -U your_username -d your_database

-- Add the SecurityStamp column
ALTER TABLE "Users" ADD COLUMN "SecurityStamp" VARCHAR(100) NOT NULL DEFAULT '';

-- Generate unique stamps for existing users
UPDATE "Users" SET "SecurityStamp" = gen_random_uuid()::text WHERE "SecurityStamp" = '';

-- Verify
SELECT "UserId", "Username", "SecurityStamp" FROM "Users";
```

### Step 3: Restart the Backend
```bash
# If running as a service
sudo systemctl start pureledger-backend
sudo systemctl status pureledger-backend

# Or if running manually
cd Pureledger-Backend
dotnet run
```

### Step 4: Verify the Fix

**Test 1: Check JWT Token Contains SecurityStamp**
```bash
# Login and capture the token
curl -X POST http://localhost:5104/api/AccountApi/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin"}'

# Decode the JWT token at https://jwt.io to verify it contains:
# - SecurityStamp claim
```

**Test 2: Password Change Invalidates Sessions**
```bash
# 1. Login on "Browser A" (save the token)
TOKEN_A=$(curl -X POST http://localhost:5104/api/AccountApi/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"oldpass"}' \
  | jq -r '.token')

# 2. Use the token (should work)
curl http://localhost:5104/api/AccountApi/me \
  -H "Authorization: Bearer $TOKEN_A"

# 3. Change password on "Browser B"
curl -X PUT http://localhost:5104/api/AdminApi/users/123 \
  -H "Authorization: Bearer $TOKEN_B" \
  -H "Content-Type: application/json" \
  -d '{"userId":123,"password":"newpass","confirmPassword":"newpass",...}'

# 4. Try to use old token from "Browser A" (should fail with 401)
curl http://localhost:5104/api/AccountApi/me \
  -H "Authorization: Bearer $TOKEN_A"
# Expected: {"success":false,"message":"Invalid token: security stamp mismatch. Please log in again."}
```

## User Impact

### Existing Users
- **Current sessions**: Will continue working until token expires naturally
- **After password change**: All sessions will be invalidated immediately
- **Action required**: None initially, but will need to re-login after password changes

### New Users
- Automatically get a SecurityStamp on creation
- Standard login flow works as before

## Monitoring

After deployment, monitor logs for:
```
Password changed for user {UserId}. All tokens invalidated.
```

This confirms the security mechanism is working.

## Rollback (If Needed)

If any issues occur:

1. **Quick Fix** - Disable middleware temporarily:
   ```csharp
   // In Program.cs, comment out:
   // app.UseSecurityStampValidation();
   ```

2. **Full Rollback**:
   ```bash
   git revert <commit-hash>
   dotnet ef database update <previous-migration>
   ```

## Expected Behavior After Fix

### ✅ Normal Operations (Should Work)
- Login with username/password
- Use JWT for API requests
- Refresh tokens before expiry
- Change profile info (name, email)
- Admin operations

### ✅ Security Operations (Should Invalidate)
- Change password → All sessions invalidated
- User deactivated → Immediate logout
- SecurityStamp mismatch → 401 Unauthorized

### ❌ Should Fail with 401
- Using JWT after password change
- Using refresh token after password change
- Using JWT for deactivated user
- Using JWT with invalid SecurityStamp

## Troubleshooting

### Issue: Migration fails
```
Check database connection
Ensure EF tools are installed: dotnet tool install --global dotnet-ef
Run: dotnet ef migrations list to see status
```

### Issue: All users getting 401 after deployment
```
Verify SecurityStamp column was added
Check all users have non-empty SecurityStamp values
Restart the backend application
```

### Issue: Password changes not invalidating sessions
```
Check logs for "Password changed for user" message
Verify SecurityStamp is being updated in database
Verify middleware is registered in Program.cs
```

## Security Considerations

1. **Performance**: Middleware queries database on each request. For high-traffic apps, consider caching.
2. **Logging**: All password changes are logged. Monitor for suspicious activity.
3. **Token Expiry**: Keep JWT expiry short (current: 24 hours) for additional security.
4. **HTTPS**: Ensure production uses HTTPS to prevent token interception.

## Support

For issues or questions:
1. Check logs: `sudo journalctl -u pureledger-backend -f`
2. Review `SECURITY_FIX_IMPLEMENTATION.md` for detailed implementation
3. Test endpoints using the verification steps above

## Summary

This fix addresses a critical security vulnerability. After deployment:
- ✅ Password changes invalidate all existing sessions
- ✅ Stolen tokens become useless after password reset
- ✅ Users maintain control of their accounts
- ✅ Security events are logged for audit

**Estimated Downtime**: < 5 minutes (for database migration and restart)

**Risk Level**: Low (backwards compatible, no data loss, can be rolled back)
