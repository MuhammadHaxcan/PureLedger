using PureLedger.Models.Master;

namespace PureLedger.Services.Auth
{
    /// <summary>
    /// Interface for system admin authentication operations.
    /// </summary>
    public interface ISystemAdminAuthService
    {
        /// <summary>
        /// Validates system admin credentials.
        /// </summary>
        /// <param name="username">The admin username</param>
        /// <param name="password">The admin password</param>
        /// <returns>True if credentials are valid</returns>
        Task<bool> ValidateCredentialsAsync(string username, string password);

        /// <summary>
        /// Gets a system admin by username.
        /// </summary>
        /// <param name="username">The admin username</param>
        /// <returns>The system admin or null if not found</returns>
        Task<SystemAdmin?> GetByUsernameAsync(string username);

        /// <summary>
        /// Gets a system admin by ID.
        /// </summary>
        /// <param name="adminId">The admin ID</param>
        /// <returns>The system admin or null if not found</returns>
        Task<SystemAdmin?> GetByIdAsync(int adminId);

        /// <summary>
        /// Updates the last login time for a system admin.
        /// </summary>
        /// <param name="adminId">The admin ID</param>
        Task UpdateLastLoginAsync(int adminId);

        /// <summary>
        /// Generates a JWT token for a system admin.
        /// </summary>
        /// <param name="admin">The system admin</param>
        /// <returns>The JWT token</returns>
        string GenerateToken(SystemAdmin admin);
    }
}
