using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IReadingSheetTemplateService
    {
        public Task<ReadingSheetTemplate> Get(int id);
        public Task<List<ReadingSheetTemplate>> GetAll();
        public Task<List<ReadingSheetTemplate>> GetByUser(int userId);
        public Task<ReadingSheetTemplate> SaveUpdate(int userId, ReadingSheetTemplate template);
    }
}
