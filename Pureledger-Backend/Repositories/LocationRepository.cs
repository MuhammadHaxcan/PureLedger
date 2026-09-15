using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        private readonly LedgerDbContext _context;

        public LocationRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Location>> GetAllAsync()
        {
            return await _context.Locations
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Location?> GetByIdAsync(int id)
        {
            return await _context.Locations.FindAsync(id);
        }

        public Location Add(Location location)
        {
            _context.Locations.Add(location);
            return location;
        }

        public Location Update(Location location)
        {
            _context.Locations.Update(location);
            return location;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
                return false;

            _context.Locations.Remove(location);
            return true;
        }

        public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
        {
            var normalizedName = name.Trim().ToLower();
            return await _context.Locations
                .AsNoTracking()
                .AnyAsync(l => l.LocationName.ToLower().Trim() == normalizedName
                    && (!excludeId.HasValue || l.LocationId != excludeId.Value));
        }
    }
}
