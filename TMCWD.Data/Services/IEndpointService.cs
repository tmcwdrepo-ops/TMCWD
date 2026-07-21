namespace TMCWD.Data.Services
{
    public interface IEndpointService
    {

        public Task<TMCWD.Data.Entities.Endpoint> Get(int id);

        public Task<List<TMCWD.Data.Entities.Endpoint>> GetByType(int type);

        public Task<TMCWD.Data.Entities.Endpoint> GetByTypeAndName(int type, string name);

    }
}
