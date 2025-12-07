using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IBookerRepository
    {
        Task<List<Booker>> GetAllAsync();
        Task<Booker?> GetByIdAsync(int id);
        Task<List<Booker>> GetAllEnabledAsync();
        Task<Booker> AddAsync(Booker booker);
        Task<Booker> UpdateAsync(Booker booker);
    }
}
