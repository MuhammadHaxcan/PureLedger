using PureLedger.Models;

public interface ISignatureRepository
{
    Task<Signature?> GetByIdAsync(int id);
    Task<Signature?> GetActiveAsync();
    Task<List<Signature>> GetAllAsync();
    Signature Add(Signature signature);
    Signature Update(Signature signature);
    Task<bool> DeleteAsync(int id);
    Task SetActiveAsync(int id);
}


