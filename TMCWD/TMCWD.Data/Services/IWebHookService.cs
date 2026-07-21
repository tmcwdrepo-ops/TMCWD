using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IWebHookService
    {
        public Task<WebHook> Get(int id);

        public Task<List<WebHook>> GetAll();

        public Task<WebHook> SaveUpdate(WebHook webHook);

    }
}
