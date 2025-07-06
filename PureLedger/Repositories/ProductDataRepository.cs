using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class ProductDataRepository : IProductDataRepository
    {
        private readonly LedgerDbContext _context;

        public ProductDataRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task DeleteAsync(ProductData productData)
        {
            _context.ProductDatas.Remove(productData);
            await _context.SaveChangesAsync();
        }
    }
}