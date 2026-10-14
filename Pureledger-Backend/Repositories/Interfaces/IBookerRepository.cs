using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IBookerRepository
    {
        Task<List<Booker>> GetAllAsync();
        Task<Booker?> GetByIdAsync(int id);
        Task<List<Booker>> GetAllEnabledAsync();
        Booker Add(Booker booker);
        Booker Update(Booker booker);
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Checks if a booker with the given name exists (case-insensitive)
        /// </summary>
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
    }
}
