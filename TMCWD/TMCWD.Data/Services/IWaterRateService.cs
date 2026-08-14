using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IWaterRateService
    {
        Task<WaterRate> Get(int id);

        Task<List<WaterRate>> GetAll();

        Task<List<WaterRate>> GetByClassification(int classification);

        Task<WaterRate> GetByClassificationAndMeterSize(
            int classification,
            decimal meterSize);
    }
}