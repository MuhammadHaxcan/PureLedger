using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface IUserControllerAccessRepository
    {
        Task<List<UserControllerAccess>> GetByUserIdAsync(int userId);
        Task<List<UserControllerAccess>> GetAllAsync();
        Task<UserControllerAccess?> GetByIdAsync(int id);
        UserControllerAccess Add(UserControllerAccess access);
        UserControllerAccess Update(UserControllerAccess access);
        Task<bool> DeleteAsync(int id);
        Task<bool> DeleteByUserIdAsync(int userId);
        Task<bool> HasControllerAccessAsync(int userId, string controller);
        Task<bool> HasActionAccessAsync(int userId, string controller, string action);
        Task<List<string>> GetUserControllersAsync(int userId);
        Task<Dictionary<string, List<string>>> GetUserActionsAsync(int userId);
        Task<List<string>> GetAllControllersAsync();
        Task<Dictionary<string, List<string>>> GetAllControllerActionsAsync();
    }
}
