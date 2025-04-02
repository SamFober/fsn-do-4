using Microsoft.EntityFrameworkCore;
using WebApi.Interfaces.Repositories;
using WebApi.Models;

namespace WebApi.Repositories
{
    public class ConcessionRepository : IConcessionRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConcessionRepository> _logger;

        public ConcessionRepository(ApplicationDbContext context, ILogger<ConcessionRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ConcessionItem> GetConcessionItemById(int id)
        {
            return await _context.ConcessionItems.FindAsync(id);
        }

        public async Task<List<ConcessionItem>> GetConcessionItems()
        {
            return await _context.ConcessionItems.ToListAsync();
        }

        public async Task<ConcessionItem> CreateConcession(ConcessionItem concessionItem)
        {
            _context.ConcessionItems.Add(concessionItem);
            await _context.SaveChangesAsync();
            return concessionItem;
        }
    }
}