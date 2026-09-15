using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

public class SignatureRepository : ISignatureRepository
{
    private readonly LedgerDbContext _dbContext;

    public SignatureRepository(LedgerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Signature?> GetByIdAsync(int id)
    {
        return await _dbContext.Signatures
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Signature?> GetActiveAsync()
    {
        return await _dbContext.Signatures
            .FirstOrDefaultAsync(x => x.IsActive);
    }

    public async Task<List<Signature>> GetAllAsync()
    {
        return await _dbContext.Signatures
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedDate)
            .ToListAsync();
    }

    public Signature Add(Signature signature)
    {
        _dbContext.Signatures.Add(signature);
        return signature;
    }

    public Signature Update(Signature signature)
    {
        _dbContext.Signatures.Update(signature);
        return signature;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var signature = await GetByIdAsync(id);
        if (signature == null)
            return false;

        _dbContext.Signatures.Remove(signature);
        return true;
    }

    public async Task SetActiveAsync(int id)
    {
        // First, set all signatures to inactive
        var allSignatures = await _dbContext.Signatures.ToListAsync();
        foreach (var sig in allSignatures)
        {
            sig.IsActive = false;
        }

        // Then set the selected one as active
        var signature = await GetByIdAsync(id);
        if (signature != null)
        {
            signature.IsActive = true;
        }

    }
}


