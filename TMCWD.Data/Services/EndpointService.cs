using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;

namespace TMCWD.Data.Services
{
    public class EndpointService : IEndpointService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public EndpointService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<Entities.Endpoint> Get(int id)
        {
            var endPoint = await _context.Endpoints.Where(x => x.Id == id).FirstOrDefaultAsync();
            return endPoint;
        }

        public async Task<List<Entities.Endpoint>> GetByType(int type)
        {
            var endPoints = await _context.Endpoints.Where(x => x.GatewayType == type).ToListAsync();
            return endPoints;
        }

        public async Task<Entities.Endpoint> GetByTypeAndName(int type, string name)
        {
            var endPoint = await _context.Endpoints.Where(x=> x.GatewayType == type && x.Name.ToLower() == name.ToLower()).FirstOrDefaultAsync();
            return endPoint;
        }

        #endregion

    }
}
