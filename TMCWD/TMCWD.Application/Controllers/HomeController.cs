using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TMCWD.Administration;
using TMCWD.Model.Administrator;
using TMCWD.Application.Models;
using System.Text.Json;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class HomeController : Controller
    {
        private readonly HttpClient _client;
        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly UserTransaction _userTransaction;
        private readonly WebService _webService;
        private readonly ApplicationLoginTransaction _applicationLoginTransaction;

        public HomeController(IHttpClientFactory factory, 
            WebService webService, 
            AuthenticatedUserService authenticatedUserService, 
            UserTransaction userTransaction,
            ApplicationLoginTransaction applicationLoginTransaction)
        {
            _client = factory.CreateClient("TmcWdApi");
            _webService = webService;
            _webService.SetClient(_client);
            _authenticatedUserService = authenticatedUserService;
            _userTransaction = userTransaction;
            _applicationLoginTransaction = applicationLoginTransaction;
        }

        public IActionResult Index()
        {
            return View(new LoginViewModel() { Email = string.Empty, Password = string.Empty });
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Prevent a failed/repeated login from retaining an earlier identity.
            HttpContext.Session.Clear();
            _applicationLoginTransaction.Email = email;
            _applicationLoginTransaction.Password = password;
            User currentUser = await _applicationLoginTransaction.Login();
            
            if(currentUser.Id > 0)
            {
                // Do not retain password or remember-token material in the user's session.
                currentUser.Password = null;
                currentUser.RememberToken = null;
                _authenticatedUserService.SetUser(currentUser);
                _authenticatedUserService.SecurityStamp = _applicationLoginTransaction.SecurityStamp;
                HttpContext.Session.SetString(AuthenticatedUserService.SecurityStampSessionKey,
                    _applicationLoginTransaction.SecurityStamp!);
                HttpContext.Session.SetString(
                    AuthenticatedUserService.SessionKey,
                    JsonSerializer.Serialize(currentUser));
                switch (currentUser.Role)
                {
                    case (int)UserRole.CustomerRepresentative:
                        return RedirectToAction("Index", "CustomerSupport");
                    case (int)UserRole.SuperAdmin:
                        return RedirectToAction("Index", "Admin");
                    case (int)UserRole.Engineer:
                        return RedirectToAction("Inventory", "Engineering");
                    case (int)UserRole.CustomerServiceOic:
                        return RedirectToAction("Penalty", "Billing");
                    case (int)UserRole.Cashier:
                        return RedirectToAction("Index", "Collections");
                }

                // Do not keep an authenticated session for a role that has no
                // portal destination configured.
                HttpContext.Session.Clear();
                _authenticatedUserService.SetUser(new User());
                ModelState.AddModelError(string.Empty, "Your account role does not have a portal destination.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
            return View("Index", new LoginViewModel() { Email = email ?? string.Empty, Password = string.Empty });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            _authenticatedUserService.SetUser(new User());
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
