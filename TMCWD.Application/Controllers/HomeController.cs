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
            return View(new LoginViewModel() { Email = string.Empty, Password = "password123" });
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            _applicationLoginTransaction.Email = email;
            _applicationLoginTransaction.Password = password;
            User currentUser = await _applicationLoginTransaction.Login();
            
            if(currentUser.Id > 0)
            {
                _authenticatedUserService.SetUser(currentUser);
                switch (currentUser.Role)
                {
                    case (int)UserRole.CustomerRepresentative:
                        return RedirectToAction("Index", "CustomerSupport");
                    case (int)UserRole.SuperAdmin:
                        return RedirectToAction("Index", "Admin");
                    case (int)UserRole.Engineer:
                        return RedirectToAction("Inventory", "Engineering");
                }
            }
            return View("Index", new LoginViewModel() { Email = string.Empty, Password = string.Empty });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
