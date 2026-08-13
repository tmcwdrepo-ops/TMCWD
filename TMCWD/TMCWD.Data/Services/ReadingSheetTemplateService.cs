using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class ReadingSheetTemplateService : IReadingSheetTemplateService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public ReadingSheetTemplateService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<ReadingSheetTemplate> Get(int id)
        {
            var readingSheetTemplate = await _context.ReadingSheetTemplates.Where(x => x.Id == id).FirstOrDefaultAsync();
            return readingSheetTemplate;
        }

        public async Task<List<ReadingSheetTemplate>> GetAll()
        {
            var readingSheetTemplates = await _context.ReadingSheetTemplates.ToListAsync();
            return readingSheetTemplates;
        }

        public async Task<List<ReadingSheetTemplate>> GetByUser(int userId)
        {
            var readingSheetTemplates = await _context.ReadingSheetTemplates.Where(x => x.ReaderId == userId).ToListAsync();
            return readingSheetTemplates;
        }

        public async Task<ReadingSheetTemplate> SaveUpdate(int userId, ReadingSheetTemplate template)
        {
            if (template.Id == 0)
            {
                template.CreatedBy = userId;
                template.DateCreated = DateTime.Now;
                _context.ReadingSheetTemplates.Add(template);
            }
            else
            {
                var forUpdate = await _context.ReadingSheetTemplates.Where(x => x.Id == template.Id).FirstOrDefaultAsync();
                if (forUpdate == null) return null;

                forUpdate.Name = template.Name;
                forUpdate.ReaderId = template.ReaderId;
                forUpdate.ZoneBookId = template.ZoneBookId;
                forUpdate.IsActive = template.IsActive;
                forUpdate.UpdatedBy = userId;
                forUpdate.DateUpdated = DateTime.Now;

                template = forUpdate;
            }

            await _context.SaveChangesAsync();
            return template;
        }

        public async Task<ReadingSheetTemplate> Deactivate(int id, int userId)
        {
            var template = await _context.ReadingSheetTemplates
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (template == null)
                return null;

            template.IsActive = false;
            template.UpdatedBy = userId;
            template.DateUpdated = DateTime.Now;

            await _context.SaveChangesAsync();

            return template;
        }


        #endregion

    }
}
