using PureLedger.Models;

namespace PureLedger.Services;

/// <summary>Resolves the booker for one Account row accompanied only by Product rows.</summary>
internal static class JournalVoucherBooker
{
    private const int CustomerGroup = 1;
    private const int ProductGroup = 2;

    public static int? Resolve(IEnumerable<Account?> rowAccounts, int? fallbackBookerId)
    {
        Account? customer = null;
        var productCount = 0;

        // Count rows, not distinct accounts: two rows for the same customer do not qualify.
        foreach (var account in rowAccounts)
        {
            if (account?.AccountGroupId == CustomerGroup)
            {
                if (customer != null) return fallbackBookerId;
                customer = account;
            }
            else if (account?.AccountGroupId == ProductGroup)
            {
                productCount++;
            }
            else
            {
                return fallbackBookerId;
            }
        }

        // Existing assignments remain authoritative even if the booker was later disabled.
        return customer != null && productCount > 0
            ? customer.LinkedBookerId ?? fallbackBookerId
            : fallbackBookerId;
    }
}
