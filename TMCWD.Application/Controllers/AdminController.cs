using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TMCWD.Application.Models;
using TMCWD.Model.Administrator;
using TMCWD.Administration;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Cryptography;
using TMCWD.Services;
using TMCWD.Utility.Encryption;

namespace TMCWD.Application.Controllers
{
    public class AdminController : Controller
    {

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly UserTransaction _userTransaction;
        private readonly InspectionTypeTransaction _inspectionTypeTransaction;
        private readonly OtherFeeTypeTransaction _otherFeeTypeTransaction;
        
        public AdminController(AuthenticatedUserService authenticatedUserService, 
            UserTransaction userTransaction, 
            InspectionTypeTransaction inspectionTypeTransaction,
            OtherFeeTypeTransaction otherFeeTypeTransaction) 
        {
            _authenticatedUserService = authenticatedUserService;
            _userTransaction = userTransaction;
            _inspectionTypeTransaction = inspectionTypeTransaction;
            _otherFeeTypeTransaction = otherFeeTypeTransaction;
        }

        public async Task<IActionResult> Index(string searchString = "")
        {
            User currentUser = _authenticatedUserService.User;

            //var userList = await _userTransaction.SearchUser(searchString);
            //if (userList == null) userList = await _userTransaction.GetUsers();

            AdminViewModel model = new()
            {
                PagedUserList = new List<User>(),
                SearchString = searchString
            };

            return View(model);
        }

        public async Task<IActionResult> GetUsers(string searchString)
        {
            var userList = await _userTransaction.SearchUser(searchString);
            if (userList == null) userList = await _userTransaction.GetUsers();
            if (userList == null) return NotFound();
            return Ok(userList);
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
                ConfirmPassword = editUser?.Id > 0 ? editUser.Password : string.Empty,
                Password = editUser?.Id > 0 ? editUser.Password : string.Empty
            };


            model.Roles = _userTransaction.GetRoles();

            return View(model);
        }

        [HttpPost()]
        public async Task<IActionResult> SaveUser(AdminViewModel model)
        {

            if (model.AddEditUser == null)
                return View("AddEditUser", model);

            if (!model.ConfirmPassword.Equals(model.Password) && model.AddEditUser.Id <= 0) return BadRequest("Password verification failed.");
            if (model.AddEditUser.Id <= 0)    model.AddEditUser.Password = StringEncyption.Encrypt(model.Password);


            await _userTransaction.SaveUpdate(_authenticatedUserService.User.Id, model.AddEditUser);
            return RedirectToAction("Index", "Admin");
        }

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
