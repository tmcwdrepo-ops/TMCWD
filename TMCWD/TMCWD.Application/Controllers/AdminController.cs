using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TMCWD.Application.Models;
using TMCWD.Model.Administrator;
using TMCWD.Administration;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Cryptography;
using TMCWD.Services;
using TMCWD.Utility.Encryption;
using TMCWD.Model.Dashboard;

namespace TMCWD.Application.Controllers
{
    public class AdminController : Controller
    {

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly UserTransaction _userTransaction;
        private readonly InspectionTypeTransaction _inspectionTypeTransaction;
        private readonly OtherFeeTypeTransaction _otherFeeTypeTransaction;
        private readonly DashboardTransaction _dashboardTransaction;
        
        public AdminController(AuthenticatedUserService authenticatedUserService, 
            UserTransaction userTransaction, 
            InspectionTypeTransaction inspectionTypeTransaction,
            OtherFeeTypeTransaction otherFeeTypeTransaction,
            DashboardTransaction dashboardTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _userTransaction = userTransaction;
            _inspectionTypeTransaction = inspectionTypeTransaction;
            _otherFeeTypeTransaction = otherFeeTypeTransaction;
            _dashboardTransaction = dashboardTransaction;
        }

        public async Task<IActionResult> Index(string searchString = "")
        {
            User currentUser = _authenticatedUserService.User;

            //var userList = await _userTransaction.SearchUser(searchString);
            //if (userList == null) userList = await _userTransaction.GetUsers();

            try
            {
                return View(await _dashboardTransaction.GetServiceSummary());
            }
            catch (HttpRequestException)
            {
                ViewBag.DashboardWarning = "Live service totals are temporarily unavailable.";
                return View(new TMCWD.Model.Dashboard.ServiceDashboard());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveServiceRequest([FromBody] ServiceRequestOrderRecord request)
        {
            if (_authenticatedUserService.User == null) return Unauthorized();
            if (request.RequestId <= 0) return BadRequest("Service request is required.");
            try { return Ok(await _dashboardTransaction.SaveServiceRequest(_authenticatedUserService.User.Id, request)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AccomplishServiceRequest(long requestId)
        {
            if (_authenticatedUserService.User == null) return Unauthorized();
            if (requestId <= 0) return BadRequest("Service request is required.");
            try
            {
                await _dashboardTransaction.AccomplishServiceRequest(requestId, _authenticatedUserService.User.Id);
                return Ok(new { status = "Done" });
            }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        public async Task<IActionResult> GetUsers(string searchString)
        {
            var userList = await _userTransaction.SearchUser(searchString);
            if (userList == null) userList = await _userTransaction.GetUsers();
            if (userList == null) return NotFound();
            return Ok(userList);
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceRequestsByDescription(string? description, string status = "Pending")
        {
            if (string.IsNullOrWhiteSpace(description) && status != "Completed") return BadRequest("Description is required.");
            try { return Ok(await _dashboardTransaction.GetServiceRequestsByDescription(description, status)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceApplications(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return BadRequest("Category is required.");
            try { return Ok(await _dashboardTransaction.GetServiceApplications(category)); }
            catch (HttpRequestException) { return StatusCode(502, "Could not load service applications."); }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceInspection(long requestId)
        {
            if (requestId <= 0) return BadRequest("Service application is required.");
            try { return Ok(await _dashboardTransaction.GetServiceInspection(requestId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetApplicationPayment(long applicationId)
        {
            if (applicationId <= 0) return BadRequest("Application is required.");
            try { return Ok(await _dashboardTransaction.GetApplicationPayment(applicationId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetApplicationTicket(long applicationId)
        {
            if (applicationId <= 0) return BadRequest("Application is required.");
            try { return Ok(await _dashboardTransaction.GetApplicationTicket(applicationId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveServiceInspection([FromBody] ServiceInspectionRecord inspection)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.SaveServiceInspection(_authenticatedUserService.User.Id, inspection)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetConnectOrder(long requestId)
        {
            if (requestId <= 0) return BadRequest("Service application is required.");
            try { return Ok(await _dashboardTransaction.GetConnectOrder(requestId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveConnectOrder([FromBody] ConnectOrderRecord order)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.SaveConnectOrder(_authenticatedUserService.User.Id, order)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkOrder(string type, long requestId)
        {
            if (requestId <= 0 || type is not ("installation" or "tapping")) return BadRequest("Valid work order information is required.");
            try { return Ok(await _dashboardTransaction.GetWorkOrder(type, requestId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveWorkOrder([FromBody] ServiceWorkOrderRecord order)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.SaveWorkOrder(_authenticatedUserService.User.Id, order)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetHouseTapping(long requestId)
        {
            if (requestId <= 0) return BadRequest("Tapping application is required.");
            try { return Ok(await _dashboardTransaction.GetHouseTapping(requestId)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveHouseTapping([FromBody] HouseTappingRecord record)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            if (record.RequestId <= 0) return BadRequest("Tapping application is required.");
            try { return Ok(await _dashboardTransaction.SaveHouseTapping(_authenticatedUserService.User.Id, record)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceRequestAssignees()
        {
            try { return Ok(await _dashboardTransaction.GetServiceRequestAssignees()); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInstallationServiceRequest([FromBody] ServiceRequestOrderRecord request)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            if (request.ApplicationId <= 0) return BadRequest("Installation application is required.");
            try { return Ok(await _dashboardTransaction.CreateServiceRequest(_authenticatedUserService.User.Id, request)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountRequest request)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            if (request.ApplicationId <= 0) return BadRequest("Application is required.");
            try { return Ok(await _dashboardTransaction.ActivateAccount(_authenticatedUserService.User.Id, request)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdvanceStage([FromBody] AdvanceStageRequest request)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            if (request.ApplicationId <= 0) return BadRequest("Application is required.");
            try { return Ok(await _dashboardTransaction.AdvanceStage(_authenticatedUserService.User.Id, request)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        public IActionResult Users()
        {
            return View();
        }

        public async Task<IActionResult> AddEditUser(int editUserId = 0)
        {

            User editUser = new();
            User currentUser = _authenticatedUserService.User;

            if (editUserId > 0)
            {
                
                editUser = await _userTransaction.Get(editUserId);
            }

            AdminViewModel model = new()
            {
                AddEditUser = editUser,
                ConfirmPassword = string.Empty,
                Password = string.Empty
            };


            model.Roles = _userTransaction.GetRoles();

            return View(model);
        }

        [HttpPost()]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUser(AdminViewModel model)
        {

            if (model.AddEditUser == null)
                return View("AddEditUser", model);

            if (!model.ConfirmPassword.Equals(model.Password) && model.AddEditUser.Id <= 0) return BadRequest("Password verification failed.");
            if (model.AddEditUser.Id <= 0) model.AddEditUser.Password = model.Password;


            await _userTransaction.SaveUpdate(_authenticatedUserService.User.Id, model.AddEditUser);
            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateUser(int userId)
        {
            User currentUser = _authenticatedUserService.User;

            var user = await _userTransaction.Get(userId);
            user.IsActive = false;
            await _userTransaction.SaveUpdate(currentUser.Id, user);
            return RedirectToAction("Index", "Admin");
        }

        public async Task<IActionResult> InspectionTypes()
        {
            InspectionTypeViewModel model = new();
            User currentUser = _authenticatedUserService.User;
            ViewBag.Role = currentUser.Role;

            //var inspectionTypes = await _inspectionTypeTransaction.GetTypes();
            //if (inspectionTypes != null && inspectionTypes.Any()) model.InspectionTypes = inspectionTypes;

            return View(model);
        }

        public async Task<IActionResult> GetInspectionTypes()
        {
            var inspectionTypes = await _inspectionTypeTransaction.GetTypes();
            if(inspectionTypes == null || !inspectionTypes.Any()) return NotFound();
            return Ok(inspectionTypes);
        }

        public async Task<IActionResult> AddEditInspectionType(int inspectionTypeId = 0)
        {
            InspectionTypeViewModel model = new();
            model.CurrentUser = _authenticatedUserService.User;

            model.AddEditInspectionType = new();
            if (inspectionTypeId > 0)
            {
                model.AddEditInspectionType = await _inspectionTypeTransaction.Get(inspectionTypeId);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveUpdateInspectionType(InspectionTypeViewModel model)
        {
            User currentUser = _authenticatedUserService.User;
            
            if(model.AddEditInspectionType.Id <= 0)
            {
                model.AddEditInspectionType.CreatedBy = currentUser.Id;
                model.AddEditInspectionType.DateCreated = DateTime.Now;
            }
            else
            {
                model.AddEditInspectionType.DateUpdated = DateTime.Now;
                model.AddEditInspectionType.UpdatedBy = currentUser.Id;
            }
            await _inspectionTypeTransaction.SaveUpdate(currentUser.Id, model.AddEditInspectionType);
            return RedirectToAction("InspectionTypes", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateInspectionType(int inspectionTypeId)
        {
            InspectionType inspType = new();
            inspType = await _inspectionTypeTransaction.Get(inspectionTypeId);
            if (inspType != null)
            {
                inspType.IsActive = false;
                inspType.UpdatedBy = _authenticatedUserService.User.Id;
                inspType.DateUpdated = DateTime.Now;
                await _inspectionTypeTransaction.SaveUpdate(_authenticatedUserService.User.Id, inspType);
            }
            return RedirectToAction("InspectionTypes", "Admin");
        }

        public async Task<IActionResult> OtherFeeTypes()
        {
            OtherFeeTypeViewModel model = new();
            model.OtherFeeTypes = new();
            return View("OtherFeeTypes", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetOtherFeeTypes()
        {

            var otherFeeTypes = await _otherFeeTypeTransaction.GetAll();
            if (otherFeeTypes == null || otherFeeTypes.Count <= 0) return NoContent();

            return Ok(otherFeeTypes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveOtherFeeType([FromBody]  OtherFeeType otherFeeType)
        {
            var updatedOtherFeeType = await _otherFeeTypeTransaction.SaveUpdate(_authenticatedUserService.User.Id, otherFeeType);
            if (updatedOtherFeeType == null) return NoContent();
            return Ok(updatedOtherFeeType);
        }

        public async Task<IActionResult> SaveUpdateOtherFeeType(OtherFeeTypeViewModel model)
        {
            var otherFeeTypeUpdate = await _otherFeeTypeTransaction.SaveUpdate(_authenticatedUserService.User.Id, model.AddEditOtherViewType);
            if(otherFeeTypeUpdate == null) return NoContent();
            return RedirectToAction("OtherFeeTypes", "Admin");
        }

        [HttpGet]
        public async Task<IActionResult> GetUsersByRole(int role)
        {
            UserRole userRole = (UserRole)role;
            var users = await _userTransaction.GetUsersByRole(userRole);
            if(users == null) return NotFound();
            return Ok(users);
        }

    }

}
