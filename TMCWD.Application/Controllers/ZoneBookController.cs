using Microsoft.AspNetCore.Mvc;
using TMCWD.Billing;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class ZoneBookController : Controller
    {

        #region fields

        private readonly AuthenticatedUserService _authUserService;
        private readonly ZoneBookTransaction _zoneBookTrans;

        #endregion

        #region constructors

        public ZoneBookController(AuthenticatedUserService authUserService, ZoneBookTransaction zoneBookTrans)
        {
            _authUserService = authUserService;
            _zoneBookTrans = zoneBookTrans;
        }

        #endregion

        #region methods

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetZone()
        {
            var zones = await _zoneBookTrans.GetZones();
            if (zones == null) return NotFound();
            return Ok(zones);
        }

        [HttpGet]
        public async Task<IActionResult> GetBooksByZone(int zone)
        {
            var books = await _zoneBookTrans.GetBooksByZone(zone);
            if(books == null) return NotFound();
            return Ok(books);
        }

        #endregion

    }
}
