using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface ILocationRepository
    {
        Task<List<Location>> GetAllAsync();
        Task<Location?> GetByIdAsync(int id);
        Task<Location> AddAsync(Location location);
        Task<Location> UpdateAsync(Location location);
    }
}