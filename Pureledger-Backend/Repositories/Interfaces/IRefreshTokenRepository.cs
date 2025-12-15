using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetActiveTokenByUserIdAsync(int userId);
        Task<List<RefreshToken>> GetAllActiveTokensByUserIdAsync(int userId);
        Task<RefreshToken> AddAsync(RefreshToken refreshToken);
        Task<RefreshToken> UpdateAsync(RefreshToken refreshToken);
        Task<bool> RevokeTokenAsync(string token, string? revokedByIp = null);
        Task<bool> RevokeAllUserTokensAsync(int userId, string? revokedByIp = null);
        Task DeleteExpiredTokensAsync();
    }
}
