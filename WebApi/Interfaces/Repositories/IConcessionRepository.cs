using WebApi.Models;

namespace WebApi.Interfaces.Repositories
{
    public interface IConcessionRepository
    {
        Task<ConcessionItem> GetConcessionItemById(int id);
        Task<List<ConcessionItem>> GetConcessionItems();
        Task<ConcessionItem> CreateConcession(ConcessionItem concessionItem);
    }
}
