using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class TariffService : ITariffService
    {
        private readonly UserDbContext _context;

        public TariffService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<Tariff> Get(int id)
        {
            return await _context.Tariffs
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<Tariff>> GetAll()
        {
            return await _context.Tariffs
                .OrderBy(x => x.Classification)
                .ThenBy(x => x.MeterSize)
                .ToListAsync();
        }

        public async Task<List<Tariff>> GetByClassification(int classification)
        {
            return await _context.Tariffs
                .Where(x =>
                    x.Classification == classification &&
                    x.IsActive)
                .OrderBy(x => x.MeterSize)
                .ToListAsync();
        }
    }
}