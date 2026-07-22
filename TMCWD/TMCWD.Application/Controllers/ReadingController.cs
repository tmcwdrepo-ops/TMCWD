using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class ReadingController : Controller
    {

        #region fields

        private readonly AuthenticatedUserService _userService;
        private readonly AccountTransaction _acctTrans;
        private readonly ReadingTransaction _readingTrans;

        #endregion

        #region constructors

        public ReadingController(AuthenticatedUserService userService, AccountTransaction acctTrans, ReadingTransaction readingTrans)
        {
            _userService = userService;
            _acctTrans = acctTrans;
            _readingTrans = readingTrans;
        }

        #endregion

        #region methods

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateForReadingByZoneBookId(int zoneBookId, DateTime billingPeriod, int readerId)
        {

            if (billingPeriod <= DateTime.Now) return BadRequest("Billing period should not be earlier than today");
            if (readerId <= 0) return BadRequest("Should assign a meter reader for this reading sheet");

            var accounts = await _acctTrans.GetByZoneBookId(zoneBookId);

            var readings = (from account in accounts
                           select new Reading
                           {
                               AccountId = account.Id,
                               BillingPeriod = billingPeriod,
                               CreatedBy = _userService.User.Id,
                               CurrentReading = 0,
                               DateCreated = DateTime.Now,
                               DateUpdated = DateTime.Now,
                               ReaderId = readerId,
                               UpdatedBy = _userService.User.Id,
                               ZoneBookId = zoneBookId
                           }).ToList();

            var savedReadings = await _readingTrans.SaveRange(readings);
            if (savedReadings == null) return BadRequest();
            return Ok(savedReadings);
        }

        #endregion

    }
}
