using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class TariffService : ITariffService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public TariffService(UserDbContext context) 
        { 
            _context = context; 
        }

        #endregion

        #region methods

        public async Task<Tariff> Get(int id)
        {
            var tariff = await _context.Tariffs.Where(x => x.Id == id).FirstOrDefaultAsync();
            return tariff;
        }

        public async Task<List<Tariff>> GetAll()
        {
            var tariffs = await _context.Tariffs.ToListAsync();
            return tariffs;
        }

        public async Task<List<Tariff>> GetByClassification(int classification)
        {
            var tariffs = await _context.Tariffs.Where(x => x.Classification == classification).ToListAsync();
            return tariffs;
        }

        #endregion

    }
}
