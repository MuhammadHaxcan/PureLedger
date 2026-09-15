using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface ILocationRepository
    {
        Task<List<Location>> GetAllAsync();
        Task<Location?> GetByIdAsync(int id);
        Location Add(Location location);
        Location Update(Location location);
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Checks if a location with the given name exists (case-insensitive)
        /// </summary>
        Task<bool> ExistsByNameAsync(string name, int? excludeId = null);
    }
}
