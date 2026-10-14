using Microsoft.EntityFrameworkCore;
using PureLedger.Exceptions;
using PureLedger.Data;

namespace PureLedger.Services;

internal static class ApprovalPostingUser
{
    // Submission policy uses the current user record, not an older JWT role.
    public static async Task<bool> IsAdminAsync(LedgerDbContext context, int userId)
    {
        var isAdmin = await context.Users.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => (bool?)x.IsAdmin).SingleOrDefaultAsync();
        return isAdmin ?? throw new NotAuthenticatedException("The submitting user is missing or inactive.");
    }
}
