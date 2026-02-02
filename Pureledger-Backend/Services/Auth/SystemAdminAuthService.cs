using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PureLedger.Data;
using PureLedger.Models.Master;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PureLedger.Services.Auth
{
    /// <summary>
    /// Service for system admin authentication.
    /// </summary>
    public class SystemAdminAuthService : ISystemAdminAuthService
    {
        private readonly MasterDbContext _masterDb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SystemAdminAuthService> _logger;

        public SystemAdminAuthService(
            MasterDbContext masterDb,
            IConfiguration configuration,
            ILogger<SystemAdminAuthService> logger)
        {
            _masterDb = masterDb;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            _logger.LogInformation("Attempting to validate credentials for user: {Username}", username);

            var admin = await GetByUsernameAsync(username);
            if (admin == null)
            {
                _logger.LogWarning("Admin user not found: {Username}", username);
                return false;
            }

            if (!admin.IsActive)
            {
                _logger.LogWarning("Admin user is not active: {Username}", username);
                return false;
            }

            _logger.LogInformation("Found admin user: {Username}, IsActive: {IsActive}, Hash length: {HashLength}",
                admin.Username, admin.IsActive, admin.PasswordHash?.Length ?? 0);

            try
            {
                var result = BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
                _logger.LogInformation("BCrypt verification result for {Username}: {Result}", username, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BCrypt verification failed for {Username}", username);
                return false;
            }
        }

        public async Task<SystemAdmin?> GetByUsernameAsync(string username)
        {
            return await _masterDb.SystemAdmins
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());
        }

        public async Task<SystemAdmin?> GetByIdAsync(int adminId)
        {
            return await _masterDb.SystemAdmins.FindAsync(adminId);
        }

        public async Task UpdateLastLoginAsync(int adminId)
        {
            var admin = await _masterDb.SystemAdmins.FindAsync(adminId);
            if (admin != null)
            {
                admin.LastLoginAt = DateTime.UtcNow;
                await _masterDb.SaveChangesAsync();
            }
        }

        public string GenerateToken(SystemAdmin admin)
        {
            var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
            var issuer = _configuration["Jwt:Issuer"] ?? "PureLedger";
            var audience = _configuration["Jwt:Audience"] ?? "PureLedger";
            var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "1440"); // 24 hours default

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Name, admin.Username),
                new Claim(ClaimTypes.GivenName, admin.FullName),
                new Claim(ClaimTypes.Role, "SystemAdmin"),
                new Claim("user_type", "SystemAdmin")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
