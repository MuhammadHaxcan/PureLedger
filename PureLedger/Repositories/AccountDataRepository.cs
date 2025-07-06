using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountDataRepository : IAccountDataRepository
    {
        private readonly LedgerDbContext _context;

        public AccountDataRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task DeleteAsync(AccountData accountData)
        {
            _context.AccountDatas.Remove(accountData);
            await _context.SaveChangesAsync();
        }
    }
}