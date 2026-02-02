using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly TenantDbContext _context;

        public RefreshTokenRepository(TenantDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task<RefreshToken?> GetActiveTokenByUserIdAsync(int userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId &&
                            !rt.IsRevoked &&
                            rt.ExpiryDate > DateTime.UtcNow)
                .OrderByDescending(rt => rt.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<RefreshToken>> GetAllActiveTokensByUserIdAsync(int userId)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId &&
                            !rt.IsRevoked &&
                            rt.ExpiryDate > DateTime.UtcNow)
                .OrderByDescending(rt => rt.CreatedDate)
                .ToListAsync();
        }

        public async Task<RefreshToken> AddAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<RefreshToken> UpdateAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            var refreshToken = await GetByTokenAsync(token);
            if (refreshToken == null || refreshToken.IsRevoked)
                return false;

            refreshToken.IsRevoked = true;
            refreshToken.RevokedDate = DateTime.UtcNow;

            await UpdateAsync(refreshToken);
            return true;
        }

        public async Task<bool> RevokeAllUserTokensAsync(int userId)
        {
            var activeTokens = await GetAllActiveTokensByUserIdAsync(userId);

            if (activeTokens.Count == 0)
                return false;

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task DeleteExpiredTokensAsync()
        {
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiryDate < DateTime.UtcNow)
                .ToListAsync();

            if (expiredTokens.Count > 0)
            {
                _context.RefreshTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();
            }
        }
    }
}
