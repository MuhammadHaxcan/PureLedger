using PureLedger.Models;

namespace PureLedger.Repositories.Interfaces
{
    public interface ITagRepository
    {
        Task<List<Tag>> GetAllAsync();
        Task<Tag?> GetByIdAsync(int id);
        Task<Tag?> GetByNameAsync(string name);
        Task<Tag> AddAsync(Tag tag);
        Task<Tag> UpdateAsync(Tag tag);
        Task DeleteAsync(Tag tag);
        Task<bool> IsTagInUseAsync(int tagId);
        Task<List<Tag>> SearchByNameAsync(string term, int take = 20);
    }
}