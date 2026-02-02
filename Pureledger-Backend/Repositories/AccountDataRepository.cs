using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class AccountDataRepository : IAccountDataRepository
    {
        private readonly TenantDbContext _context;

        public AccountDataRepository(TenantDbContext context)
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