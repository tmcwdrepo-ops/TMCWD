using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class WebHookService : IWebHookService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public WebHookService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<WebHook> Get(int id)
        {
            var webhook = await _context.WebHooks.Where(x => x.Id == id).FirstOrDefaultAsync();
            return webhook;
        }

        public async Task<List<WebHook>> GetAll()
        {
            var webhooks = await _context.WebHooks.ToListAsync();
            return webhooks;
        }

        public async Task<WebHook> SaveUpdate(WebHook webHook)
        {
            if(webHook.Id == 0)
            {
                webHook.DateCreated = DateTime.Now;
                _context.WebHooks.Add(webHook);
            }
            
            await _context.SaveChangesAsync();

            return webHook;
        }

        #endregion
    }
}
