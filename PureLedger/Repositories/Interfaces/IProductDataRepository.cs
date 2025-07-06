using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IProductDataRepository
    {
        Task DeleteAsync(ProductData productData);
    }
}