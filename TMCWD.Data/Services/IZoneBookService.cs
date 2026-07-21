using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IZoneBookService
    {

        public Task<ZoneBook> Get(int id);

        public Task<List<ZoneBook>> GetAll();

        public Task<List<ZoneBook>> GetByZone(int zone);

        public Task<List<ZoneBook>> GetBooksByZone(int zone);

        public Task<List<ZoneBook>> GetZones();

        public Task<ZoneBook> GetByZoneAndBook(int zone, int book);

        public Task<List<ZoneBook>> GetByWeek(int week);

    }
}
