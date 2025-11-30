using PureLedger.Models;

public interface ISignatureRepository
{
    Task<Signature?> GetByIdAsync(int id);
    Task<Signature?> GetActiveAsync();
    Task<List<Signature>> GetAllAsync();
    Task<Signature> AddAsync(Signature signature);
    Task<Signature> UpdateAsync(Signature signature);
    Task<bool> DeleteAsync(int id);
    Task SetActiveAsync(int id);
}


