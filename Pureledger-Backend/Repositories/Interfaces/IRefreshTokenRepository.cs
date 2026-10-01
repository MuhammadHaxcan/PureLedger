using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetActiveTokenByUserIdAsync(int userId);
        Task<List<RefreshToken>> GetAllActiveTokensByUserIdAsync(int userId);
        RefreshToken Add(RefreshToken refreshToken);
        RefreshToken Update(RefreshToken refreshToken);
        Task<bool> RevokeTokenAsync(string token);
        Task<bool> RevokeAllUserTokensAsync(int userId);
        Task DeleteExpiredTokensAsync();
    }
}
