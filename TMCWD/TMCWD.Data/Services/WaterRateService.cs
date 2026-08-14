using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class WaterRateService : IWaterRateService
    {
        private readonly UserDbContext _context;

        public WaterRateService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<WaterRate> Get(int id)
        {
            return await _context.WaterRates
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<WaterRate>> GetAll()
        {
            return await _context.WaterRates
                .Where(x => x.IsActive)
                .OrderBy(x => x.Classification)
                .ThenBy(x => x.MeterSize)
                .ToListAsync();
        }

        public async Task<List<WaterRate>> GetByClassification(int classification)
        {
            return await _context.WaterRates
                .Where(x =>
                    x.Classification == classification &&
                    x.IsActive)
                .OrderBy(x => x.MeterSize)
                .ToListAsync();
        }

        public async Task<WaterRate> GetByClassificationAndMeterSize(
            int classification,
            decimal meterSize)
        {
            return await _context.WaterRates
                .Where(x =>
                    x.Classification == classification &&
                    x.MeterSize == meterSize &&
                    x.IsActive)
                .FirstOrDefaultAsync();
        }
    }
}