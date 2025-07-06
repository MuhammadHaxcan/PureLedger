using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Repositories
{
    public class TagRepository : ITagRepository
    {
        private readonly LedgerDbContext _context;

        public TagRepository(LedgerDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tag>> GetAllAsync()
        {
            return await _context.Tags.ToListAsync();
        }

        public async Task<Tag?> GetByIdAsync(int id)
        {
            return await _context.Tags.FindAsync(id);
        }

        public async Task<Tag?> GetByNameAsync(string name)
        {
            return await _context.Tags
                .FirstOrDefaultAsync(t => t.TagName.ToLower() == name.ToLower());
        }

        public async Task<Tag> AddAsync(Tag tag)
        {
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return tag;
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            _context.Tags.Update(tag);
            await _context.SaveChangesAsync();
            return tag;
        }

        public async Task DeleteAsync(Tag tag)
        {
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsTagInUseAsync(int tagId)
        {
            return await _context.JournalEntryTags.AnyAsync(jet => jet.JournalEntryTagId == tagId);
        }

        public async Task<List<Tag>> SearchByNameAsync(string term, int take = 20)
        {
            return await _context.Tags
                .Where(t => t.TagName.Contains(term))
                .OrderBy(t => t.TagName)
                .Take(take)
                .ToListAsync();
        }
    }
}