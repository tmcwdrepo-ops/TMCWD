using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly UserDbContext _dbContext;

        public RecommendationService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<Recommendation?> Get(int id)
        {
            var recommendation = await _dbContext.Recommendations.Where(x => x.Id == id).FirstOrDefaultAsync();
            return recommendation;
        }

        public async Task<IEnumerable<Recommendation>> GetByRequestId(int requestId)
        {
            var recommendations = _dbContext.Recommendations.Where(x => x.RequestId == requestId);
            return await recommendations.ToListAsync();
        }

        public async Task<Recommendation> SaveUpdate(int userId, int requestId, Recommendation recommendation)
        {
            recommendation.RequestId = requestId;
            if(recommendation.Id > 0)
            {
                recommendation.UpdatedBy = userId;
                recommendation.DateUpdated = DateTime.Now;
                _dbContext.Recommendations.Update(recommendation);
            }
            else
            {
                recommendation.CreatedBy = userId;
                recommendation.DateCreated = DateTime.Now;
                _dbContext.Recommendations.Add(recommendation);
            }

            await _dbContext.SaveChangesAsync();
            return recommendation;
        }

    }

}
