using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TMCWD.Administration;
using TMCWD.Application.Models;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.CustomerSupport;
using TMCWD.Services;
using System.Net;
using TMCWD.Model.Engineering;
using TMCWD.Engineering;
using TMCWD.Model.Extensions;
using TMCWD.Model.Dashboard;

namespace TMCWD.Application.Controllers
{
    public class CustomerSupportController : Controller
    {

        #region fields

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly AccountTransaction _accountTransaction;
        private readonly CustomerTransaction _customerTransaction;
        private readonly RequestTransaction _requestTransaction;
        private readonly InspectionTypeTransaction _inspectionTypeTransaction;
        private readonly UserTransaction _userTransaction;
        private readonly RecommendationTransaction _recommendationTransaction;
        private readonly InventoryTransaction _inventoryTransaction;
        private readonly MaterialTransaction _materialTransaction;
        private readonly FindingTransaction _findingTransaction;
        private readonly DashboardTransaction _dashboardTransaction;
        private readonly TMCWD.Billing.BillingTransaction _billingTransaction;

        #endregion

        #region constructors

        public CustomerSupportController(AuthenticatedUserService authenticatedUserService, 
            AccountTransaction accountTransaction, 
            CustomerTransaction customerTransaction,
            RequestTransaction requestTransaction,
            InspectionTypeTransaction inspectionTypeTransaction,
            UserTransaction userTransaction,
            RecommendationTransaction recommendationTrasaction, 
            InventoryTransaction inventoryTransaction,
            MaterialTransaction materialTransaction,
            FindingTransaction findingTransaction,
            DashboardTransaction dashboardTransaction,
            TMCWD.Billing.BillingTransaction billingTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _accountTransaction = accountTransaction;
            _customerTransaction = customerTransaction;
            _requestTransaction = requestTransaction;
            _inspectionTypeTransaction = inspectionTypeTransaction;
            _userTransaction = userTransaction;
            _recommendationTransaction = recommendationTrasaction;
            _inventoryTransaction = inventoryTransaction;
            _materialTransaction = materialTransaction;
            _findingTransaction = findingTransaction;
            _dashboardTransaction = dashboardTransaction;
            _billingTransaction = billingTransaction;
        }

        #endregion

        #region customer
        public async Task<IActionResult> Index()
        {
            CustomerViewModel model = new();
            //model.PagedCustomerList = await _customerTransaction.GetCustomers();

            return View(model);
        }

        // ── Customer Information page ─────────────────────────────────────────

        /// <summary>
        /// Main customer information page. Loads the customer and their accounts.
        /// Pass customerId=0 (default) to open a blank form ready for searching.
        /// </summary>
        public async Task<IActionResult> CustomerInformation(int customerId = 0)
        {
            var model = new CustomerInformationViewModel();

            // Always fetch the full sorted ID list so Prev / Next can work.
            var allCustomers = await _customerTransaction.GetCustomers();
            if (allCustomers != null && allCustomers.Any())
            {
                model.CustomerIds = allCustomers
                    .OrderBy(c => c.Id)
                    .Select(c => c.Id)
                    .ToList();
            }

            if (customerId > 0)
            {
                var customerTask  = _customerTransaction.Get(customerId);
                var accountsTask  = _accountTransaction.GetByCustomerId(customerId);

                await Task.WhenAll(customerTask, accountsTask);

                model.Customer         = customerTask.Result  ?? new Customer();
                model.CustomerAccounts = accountsTask.Result  ?? new List<Account>();
                model.SelectedAccount  = model.CustomerAccounts.FirstOrDefault(a => a.IsCurrentAddress)
                                         ?? model.CustomerAccounts.FirstOrDefault();
            }

            ViewData["ActivePage"] = "customers";
            return View(model);
        }

        /// <summary>
        /// AJAX endpoint – returns full customer JSON (used by the search / account-number lookup).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCustomerInformationData(int customerId)
        {
            if (customerId <= 0) return BadRequest();

            var customerTask = _customerTransaction.Get(customerId);
            var accountsTask = _accountTransaction.GetByCustomerId(customerId);

            await Task.WhenAll(customerTask, accountsTask);

            var customer = customerTask.Result;
            var accounts = accountsTask.Result ?? new List<Account>();

            if (customer == null) return NotFound();

            return Ok(new
            {
                customer,
                accounts,
                selectedAccount = accounts.FirstOrDefault(a => a.IsCurrentAddress) ?? accounts.FirstOrDefault()
            });
        }

        /// <summary>
        /// AJAX – saves the customer portion of the Customer Information form.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveCustomerInformation([FromBody] Customer customer)
        {
            if (customer == null) return BadRequest("Customer data is required.");

            var saved = await _customerTransaction.SaveUpdate(_authenticatedUserService.User.Id, customer);
            if (saved == null) return StatusCode(500, "Customer could not be saved.");

            return Ok(saved);
        }

        /// <summary>
        /// AJAX – saves the account (meter information) portion of the Customer Information form.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveAccountInformation([FromBody] Account account)
        {
            if (account == null) return BadRequest("Account data is required.");

            var saved = await _accountTransaction.SaveUpdate(_authenticatedUserService.User.Id, account);
            if (saved == null) return StatusCode(500, "Account could not be saved.");

            return Ok(saved);
        }

        public async Task<IActionResult> GetCustomers()
        {
            var customers = await _customerTransaction.GetCustomers();
            if (customers == null || !customers.Any()) return NotFound();
            return Ok(customers);
        }

        /// <summary>
        /// AJAX – returns billing history for a given account number.
        /// Proxies /Billing/History so the Customer Information page doesn't need a cross-controller fetch.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBillsData(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber)) return BadRequest();

            var history = await _billingTransaction.GetBillingHistory(accountNumber);
            return Ok(history ?? new List<TMCWD.Model.Billing.BillingHistory>());
        }
        [HttpGet]
        public async Task<IActionResult> SearchByAccount(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<object>());

            var accounts = await _accountTransaction.Search(q);
            if (accounts == null || !accounts.Any()) return Ok(new List<object>());

            // Fetch unique customers for matched accounts
            var customerIds = accounts.Select(a => a.CustomerId).Distinct().ToList();
            var customers   = await _customerTransaction.GetCustomersFromIds(customerIds);
            var customerMap = (customers ?? new List<Customer>())
                               .ToDictionary(c => c.Id);

            var results = accounts
                .Where(a => customerMap.ContainsKey(a.CustomerId))
                .Select(a =>
                {
                    var c = customerMap[a.CustomerId];
                    return new
                    {
                        customerId    = c.Id,
                        accountId     = a.Id,
                        accountNumber = a.AccountNumber,
                        meterNumber   = a.MeterNumber,
                        customerName  = $"{c.Lastname}, {c.Firstname} {c.Middlename}".Trim(),
                        address       = a.FullAddress,
                    };
                })
                .ToList();

            return Ok(results);
        }

        public async Task<IActionResult> AddEditCustomer(int editCustomerId = 0)
        {
            User currentUser = _authenticatedUserService.User;

            CustomerViewModel model = new()
            {
                AddEditCustomer = await _customerTransaction.Get(editCustomerId) ?? new Customer(),
                CustomerAccounts = editCustomerId > 0 ? await _accountTransaction.GetByCustomerId(editCustomerId) : new List<Account>()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCustomer(CustomerViewModel model)
        {

            if (model.AddEditCustomer == null) return BadRequest("Customer data is required");

            await _customerTransaction.SaveUpdate(_authenticatedUserService.User.Id, model.AddEditCustomer);

            return RedirectToAction("Index", "CustomerSupport");
        }

        public async Task<IActionResult> DeactivateCustomer(int customerId)
        {
            Customer cust = new();
            User currentUser = new();
            currentUser = _authenticatedUserService.User;
            cust = await _customerTransaction.Get(customerId);

            if (cust != null)
            {
                cust.IsActive = false;
                await _customerTransaction.SaveUpdate(currentUser.Id, cust);
            }

            return RedirectToAction("Index", "CustomerSupport");
        }

        public async Task<IActionResult> GetCustomerName(int customerId)
        {
            var customer = await _customerTransaction.Get(customerId);
            return ViewComponent("CustomerName", $"{customer.Firstname} {customer.Lastname}");
        }

        [HttpGet]
        public async Task<IActionResult> SearchCustomer(string searchString)
        {
            var customers = await _customerTransaction.Search(searchString);
            if (customers == null || !customers.Any()) return NotFound();
            return Ok(customers);
        }

        public IActionResult RefreshCustomerNameComponent(int customerId, string pId = "", string pClass = "")
        {
            return ViewComponent("CustomerName", new { customerId = customerId, pId = pId, pClass = pClass });
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerById(int customerId)
        {

            Customer customer = new();
            customer = await _customerTransaction.Get(customerId);

            return View(customer);
        }

        #endregion

        #region account

        public IActionResult AddEditAccount()
        {

            return View();
        }

        public async Task<IActionResult> DeactivateAccount(int accountId)
        {
            Account acct = new();
            acct = await _accountTransaction.Get(accountId);

            if (acct != null)
            {
                acct.Status = AccountStatus.Closed;
                await _accountTransaction.SaveUpdate(_authenticatedUserService.User.Id, acct);
            }

            return RedirectToAction("AddEditCustomer", "CustomerSupport", new { editCustomerId = acct?.CustomerId ?? 0 });
        }

        public IActionResult RefreshAccountSelectComponent(int customerId)
        {
            return ViewComponent("AccountSelection", new { customerId = customerId });
        }

        #endregion

        #region user

        [HttpGet]
        public async Task<IActionResult> GetUserById(int userId)
        {
            User user = new();
            user = await _userTransaction.Get(userId);
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserByIdForTable(int userId)
        {
            User user = new();
            user = await _userTransaction.Get(userId);
            if (user == null) return NoContent();
            return Ok(user);
        }

        #endregion

        #region request

        public async Task<IActionResult> Requests()
        {
            User currentUser = _authenticatedUserService.User;
            RequestViewModel model = new();
            model.CurrentUser = currentUser;
            ViewBag.Role = currentUser.Role;
            //model.Requests = await _requestTransaction.GetRequests();
            return View(model);
        }

        public async Task<IActionResult> AddEditRequest(int requestId = 0)
        {
            User currentUser = _authenticatedUserService.User;
            RequestViewModel model = new();
            model.CurrentUser = currentUser;

            List<RequestDetail> requestDetails = new();
            List<InspectionType> types = new();

            Task<List<InspectionType>> getInspectionTypesTask = _inspectionTypeTransaction.GetTypes();
            Task<List<Inventory>> getInventoryTask = _inventoryTransaction.GetAll();

            await Task.WhenAll(getInspectionTypesTask, getInspectionTypesTask);

            types = getInspectionTypesTask.Result ?? new();

            model.InventoryItems = getInventoryTask.Result ?? new();

            if (requestId > 0)
            {
                model.AddEditRequest = await _requestTransaction.Get(requestId);
                if (model.AddEditRequest != null)
                {
                    if (model.AddEditRequest.IsServiceRequest) return RedirectToAction("Index", "Admin");
                    Task<Customer> getCustomerTask = model.AddEditRequest.CustomerId is > 0
                        ? _customerTransaction.Get(model.AddEditRequest.CustomerId.Value)
                        : Task.FromResult(new Customer());
                    Task<Account> getAccountTask = model.AddEditRequest.AccountId is > 0
                        ? _accountTransaction.Get(model.AddEditRequest.AccountId.Value)
                        : Task.FromResult(new Account());
                    Task<List<RequestDetail>> getRequestDetailsTask = _requestTransaction.GetRequestDetailByRequestId(model.AddEditRequest.Id);
                    Task<List<Recommendation>> getRecommendationsTask = _recommendationTransaction.GetByRequestId(model.AddEditRequest.Id);
                    //Task<List<Material>> getMaterial = _materialTransaction.GetByJobOrderIdId(model.AddEditRequest.Id);

                    await Task.WhenAll(getCustomerTask, getAccountTask, getRequestDetailsTask, getRecommendationsTask);
                    model.CurrentCustomer = getCustomerTask.Result;
                    model.CurrentAccount = getAccountTask.Result;
                    requestDetails = getRequestDetailsTask.Result;
                    model.Recommendations = getRecommendationsTask.Result;
                    //if (model.InventoryItems == null || materials == null) model.Materials = new();
                    //else model.Materials = GetMaterialDetails(materials, model.InventoryItems);
                }
            }

            //model.InspectionTypes = ConvertToInspectionTypeToViewModel(types, requestDetails);

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveRequest([FromBody] object data)
        {
            User currentUser = _authenticatedUserService.User;
            string? stringRequest = data?.ToString();

            List<int> detailIds = new List<int>();

            if (String.IsNullOrEmpty(stringRequest?.Trim())) return BadRequest("Request data is empty");

            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            Request? request = JsonSerializer.Deserialize<Request>(stringRequest, serializerOptions);

            if (request == null) return BadRequest("Cannot convert request data to request object");

            request.ControlNumber = "TKT";

            var updatedRequest = await _requestTransaction.SaveUpdate(currentUser.Id, request);

            return Ok(updatedRequest.Id);
        }

        [HttpPost]
        public async Task<IActionResult> SaveRequestDetails(int requestId, [FromBody] object data)
        {
            User currentUser = _authenticatedUserService.User;
            if (data == null) return BadRequest();
            string stringRequest = data.ToString() ?? string.Empty; 

            List<int> detailIds = new List<int>();
             
            if (data == null) return BadRequest("No selected service(s)");

            string listIds = data.ToString() ?? string.Empty;

            if (String.IsNullOrEmpty(listIds.Trim())) return BadRequest("No selected service(s)");

            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            List<RequestDetail> requestDetails = JsonSerializer.Deserialize<List<RequestDetail>>(listIds) ?? new();

            if (requestDetails == null || !requestDetails.Any()) return BadRequest("One of the services id is not in the correct format");

            foreach (var requestDetail in requestDetails)
            {
                requestDetail.RequestId = requestId;
            }

            await _requestTransaction.DeleteRequestDetails(requestId);

            var res = await _requestTransaction.SaveMulipleRequestDetail(currentUser.Id, requestId, requestDetails);

            return Ok(res);
        }

        #endregion

        #region material

        [HttpPost]
        //public async Task<IActionResult> SaveMaterial(int jobOrderId, [FromBody] object data)
        //{
        //    if (jobOrderId == 0 || data == null) return BadRequest();

        //    string content = JsonSerializer.Serialize(data);

        //    if (String.IsNullOrEmpty(content.Trim())) return NoContent();

        //    using var doc = JsonDocument.Parse(content);
        //    var root = doc.RootElement;

        //    if (root.ValueKind == JsonValueKind.Null) return NoContent();

        //    var inventoryIdProp = root.GetProperty("inventoryId");
        //    var quantityProp = root.GetProperty("quantity");

        //    if (inventoryIdProp.ValueKind == JsonValueKind.Null || quantityProp.ValueKind == JsonValueKind.Null) return NoContent();

        //    int invId = inventoryIdProp.GetInt32();
        //    int quant = quantityProp.GetInt32();
        //    //int.TryParse(inventoryIdProp.GetString(), out int inventoryId);
        //    //int.TryParse(quantityProp.GetString(), out int quantity);

        //    Task<Inventory> getInventoryItemTask = _inventoryTransaction.Get(invId);
        //    Task<List<Material>> getRequestMaterialsTask = _materialTransaction.GetByJobOrderId(jobOrderId);

        //    await Task.WhenAll(getInventoryItemTask, getRequestMaterialsTask);

        //    Inventory inventoryItem = getInventoryItemTask.Result;
        //    List<Material> requestMaterials = getRequestMaterialsTask.Result;

        //    if (requestMaterials != null && requestMaterials.Any())
        //    {
        //        var existingMaterial = requestMaterials.Where(x => x.InventoryId == invId).FirstOrDefault();
        //        if (existingMaterial != null) return NoContent();
        //    }

        //    Material material = new()
        //    {
        //        InventoryId = invId,
        //        UnitSellingPrice = inventoryItem.UnitSellingPrice,
        //        RequestedQuantity = quant,
        //    };

        //    var savedMaterial = await _materialTransaction.SaveUpdate(_authenticatedUserService.User.Id, jobOrderId, material);

        //    return Ok(new { material = savedMaterial, inventory = inventoryItem });

        //}

        [HttpPost]
        public async Task<IActionResult> SaveMaterial(int jobOrderId, [FromBody] Material material)
        {
            var savedMaterial = await _materialTransaction.SaveUpdate(_authenticatedUserService.User.Id, jobOrderId, material);
            if (savedMaterial == null) return NoContent();
            return Ok(savedMaterial);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteMaterial(int jobOrderId, int id)
        {
            var isSuccess = await _materialTransaction.DeleteMaterial(jobOrderId, id);
            return Ok(isSuccess);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateUnitCostQuantity([FromBody] object param)
        {

            if (param == null) return NoContent();
            string jsonData = JsonSerializer.Serialize(param);

            if (String.IsNullOrEmpty(jsonData.Trim())) return NoContent();

            using var doc = JsonDocument.Parse(jsonData);

            if (doc == null) return NoContent();

            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Null) return NoContent();
            // int inventoryId, int requestId, int id, int quantity = 0, float unitCost = 0

            var inventoryIdProp = root.GetProperty("inventoryId");
            var requestIdProp = root.GetProperty("requestId");
            var idProp = root.GetProperty("id");
            var quantityProp = root.GetProperty("quantity");
            var unitCostProp = root.GetProperty("unitCost");

            if (inventoryIdProp.ValueKind == JsonValueKind.Null || requestIdProp.ValueKind == JsonValueKind.Null || idProp.ValueKind == JsonValueKind.Null || quantityProp.ValueKind == JsonValueKind.Null || unitCostProp.ValueKind == JsonValueKind.Null) return NoContent();

            int id = idProp.GetInt32();
            int requestId = requestIdProp.GetInt32();
            int inventoryId = inventoryIdProp.GetInt32();
            int quantity = quantityProp.GetInt32();
            float unitCost = (float)unitCostProp.GetDouble();

            Task<Material> patchQuantityOrNewUnitCost = _materialTransaction.UpdateQuantityOrNewUnitCost(_authenticatedUserService.User.Id, requestId, id, quantity, unitCost);
            Task<Inventory> getInventory = _inventoryTransaction.Get(inventoryId);

            await Task.WhenAll(patchQuantityOrNewUnitCost, getInventory);

            var newMaterial = patchQuantityOrNewUnitCost.Result;
            var inventory = getInventory.Result;


            return Ok(new { material = newMaterial, inventory = inventory });
        }

        #endregion

        #region recommendation

        [HttpPost]
        public async Task<IActionResult> SaveRecommendation([FromBody] object data)
        {

            if (data == null) return NoContent();
            string stringRequest = JsonSerializer.Serialize(data);

            if (String.IsNullOrEmpty(stringRequest.Trim())) return NoContent();

            using var doc = JsonDocument.Parse(stringRequest);
            if (doc == null) return NoContent();

            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Null) return NoContent();
            var requestIdAttr = root.GetProperty("requestId");
            var detailsAttr = root.GetProperty("details");

            if (requestIdAttr.ValueKind == JsonValueKind.Null || detailsAttr.ValueKind == JsonValueKind.Null) return NoContent();
            int.TryParse(requestIdAttr.GetString(), out int requestId);
            string details = detailsAttr.GetString() ?? string.Empty;

            Recommendation recommendation = new()
            {
                RequestId = requestId,
                Details = WebUtility.UrlDecode(details)
            };

            recommendation.DateCreated = DateTime.Now;
            recommendation.DateUpdated = DateTime.Now;

            var updatedRecommendation = await _recommendationTransaction.SaveUpdate(recommendation.RequestId, _authenticatedUserService.User.Id, recommendation);

            return Ok(updatedRecommendation);
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterialRequest(int jobOrderId)
        {
            Task<List<Material>> getMaterials = _materialTransaction.GetByJobOrderId(jobOrderId);
            Task<List<Inventory>> getInventory = _inventoryTransaction.GetAll();
            await Task.WhenAll(getMaterials, getInventory);
            var materials = getMaterials.Result;
            var inventory = getInventory.Result;
            if (materials == null || !materials.Any()) return NoContent();

            var materialInventory = from mats in materials
                                    join invItems in inventory on mats.InventoryId equals invItems.Id
                                    select new
                                    {
                                        Id = mats.Id,
                                        InventoryId = invItems.Id,
                                        Name = invItems.Name,
                                        OnHand = invItems.Quantity,
                                        RequestedQuantity = mats.RequestedQuantity,
                                        SellingPrice = invItems.UnitSellingPrice,
                                        UOM = invItems.UOM
                                    };
            return Ok(materialInventory);
        }

        #endregion

        #region new connection

        public IActionResult NewConnection()
        {
            return View(new NewConnectionApplicationRecord { DateApplied = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNewConnection([FromBody] NewConnectionApplicationRecord application)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            if (string.IsNullOrWhiteSpace(application.ApplicantName)) return BadRequest("Applicant name is required.");
            if (string.IsNullOrWhiteSpace(application.Barangay)) return BadRequest("Barangay is required.");
            if (string.IsNullOrWhiteSpace(application.Address)) return BadRequest("Service address is required.");
            if (string.IsNullOrWhiteSpace(application.MobileNumber)) return BadRequest("Mobile number is required.");

            try
            {
                return Ok(await _dashboardTransaction.CreateNewConnection(
                    _authenticatedUserService.User.Id, application));
            }
            catch (HttpRequestException exception)
            {
                return BadRequest(exception.Message);
            }
        }

        public IActionResult Disconnection()
        {
            return View(new DisconnectionOrderRecord
            {
                JobOrderDate = DateTime.Today,
                DisconnectionDate = DateTime.Today,
                ReconnectionFee = 350m,
                ChargeFee = true
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchDisconnectionAccounts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(new List<DisconnectionAccountRecord>());
            try { return Ok(await _dashboardTransaction.SearchDisconnectionAccounts(q)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpGet]
        public async Task<IActionResult> GetDisconnectionOrders()
        {
            try { return Ok(await _dashboardTransaction.GetDisconnectionOrders()); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDisconnectionOrder([FromBody] DisconnectionOrderRecord order)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.SaveDisconnectionOrder(_authenticatedUserService.User.Id, order)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        public IActionResult AccountTransfer()
        {
            return View(new AccountTransferRecord
            {
                JobOrderDate = DateTime.Today,
                TransferDate = DateTime.Today
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchAccountTransferAccounts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(new List<AccountTransferLookupRecord>());
            try { return Ok(await _dashboardTransaction.SearchAccountTransferAccounts(q)); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccountTransfer([FromBody] AccountTransferRecord transfer)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.SaveAccountTransfer(_authenticatedUserService.User.Id, transfer)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        public IActionResult ServiceRequest()
        {
            ViewBag.ReceivedBy = _authenticatedUserService.User.Name ?? string.Empty;
            return View(new ServiceRequestOrderRecord
            {
                JobOrderDate = DateTime.Today,
                DateReferred = DateTime.Today
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceRequestAssignees()
        {
            try { return Ok(await _dashboardTransaction.GetServiceRequestAssignees()); }
            catch (HttpRequestException exception) { return StatusCode(502, exception.Message); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveServiceRequest([FromBody] ServiceRequestOrderRecord request)
        {
            if (_authenticatedUserService.User.Id <= 0) return Unauthorized();
            try { return Ok(await _dashboardTransaction.CreateServiceRequest(_authenticatedUserService.User.Id, request)); }
            catch (HttpRequestException exception) { return BadRequest(exception.Message); }
        }

        #endregion

        #region findings
        #endregion

        #region private methods

        //private List<RequestInspectionTypeViewModel> ConvertToInspectionTypeToViewModel(List<InspectionType> inspTypes, List<RequestDetail>? details)
        //{

        //    List<RequestInspectionTypeViewModel> types = new();

        //    if (inspTypes == null || inspTypes.Count <= 0) return new List<RequestInspectionTypeViewModel>();

        //    foreach(var inspType in inspTypes)
        //    {
        //        RequestInspectionTypeViewModel model = new RequestInspectionTypeViewModel
        //        {
        //            Type = inspType,
        //            IsChecked = details.Where(x => x.RequestTypeId == inspType.Id).Any()

        //        };

        //        types.Add(model);
        //    }

        //    return types;
        //}

        private List<MaterialDetailViewModel> GetMaterialDetails(List<Material> materials, List<Inventory> inventory)
        {
            List<MaterialDetailViewModel> details = (from m in materials
                                                    join i in inventory on m.InventoryId equals i.Id
                                                    select new MaterialDetailViewModel
                                                    {
                                                        InventoryItem = i,
                                                        Material = m
                                                    }).ToList();
            return details;
        }

        #endregion

    }

}
