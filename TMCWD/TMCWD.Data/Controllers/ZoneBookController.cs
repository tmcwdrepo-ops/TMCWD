using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ZoneBookController : Controller
    {
        #region fields

        private readonly IZoneBookService _service;

        #endregion

        #region constructors

        public ZoneBookController(IZoneBookService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var zoneBook = await _service.Get(id);
            if(zoneBook == null) return NotFound();
            return Ok(zoneBook);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var zoneBooks = await _service.GetAll();
            if(zoneBooks == null || !zoneBooks.Any()) return NotFound();
            return Ok(zoneBooks);
        }

        [HttpGet("GetByZone/{zone}")]
        public async Task<IActionResult> GetByZone(int zone)
        {
            var zoneBooks = await _service.GetByZone(zone);
            if (zoneBooks == null || !zoneBooks.Any()) return NotFound();
            return Ok(zoneBooks);
        }

        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var zoneBook = await _service.GetByZoneAndBook(zone, book);
            if(zoneBook == null) return NotFound(); 
            return Ok(zoneBook);
        }

        [HttpGet("GetZones")]
        public async Task<IActionResult> GetZones()
        {
            var zones = await _service.GetZones();
            if (zones == null || !zones.Any()) return NotFound();
            return Ok(zones);
        }

        [HttpGet("GetBooksByZone/{zone}")]
        public async Task<IActionResult> GetBooksByZone(int zone)
        {
            var books = await _service.GetBooksByZone(zone);
            if (books == null || !books.Any()) return NotFound();
            return Ok(books);
        }

        [HttpGet("GetByWeek/{week}")]
        public async Task<IActionResult> GetByWeek(int week)
        {
            var zoneBooks = await _service.GetByWeek(week);
            if (zoneBooks == null || !zoneBooks.Any()) return NotFound();
            return Ok(zoneBooks);
        }

        #endregion

    }
}
