using System.Security.Claims;

namespace PureLedger.Services.Interface
{
    public interface IJwtService
    {
        string GenerateToken(int userId, string username, string fullName, bool isAdmin, string subroute);
        ClaimsPrincipal? ValidateToken(string token);
    }
}

