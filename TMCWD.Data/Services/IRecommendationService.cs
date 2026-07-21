using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IRecommendationService
    {
        Task<Recommendation> SaveUpdate(int userId, int requestId, Recommendation recommendation);
        Task<Recommendation?> Get(int id);
        Task<IEnumerable<Recommendation>> GetByRequestId(int requestId);
    }
}
