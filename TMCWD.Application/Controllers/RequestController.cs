using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using TMCWD.Administration;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Extensions;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class RequestController : Controller
    {
        private readonly RequestTransaction _requestTransaction;
        private readonly InspectionTypeTransaction _inspectionTypeTransaction;
        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly CustomerTransaction _customerTransaction;
        private readonly AccountTransaction _accountTransaction;
        private readonly UserTransaction _userTransaction;
        private readonly JobOrderTransaction _jobOrderTransaction;
        private readonly ApprovalHistoryTransaction _approvalHistoryTransaction;

        public RequestController(RequestTransaction requestTransaction, 
            AuthenticatedUserService authenticatedUserService, 
            InspectionTypeTransaction inspectionTypeTransaction,
            CustomerTransaction customerTransaction,
            AccountTransaction accountTransaction,
            UserTransaction userTransaction,
            JobOrderTransaction jobOrderTransaction,
            ApprovalHistoryTransaction approvalHistoryTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _requestTransaction = requestTransaction;
            _inspectionTypeTransaction = inspectionTypeTransaction;
            _customerTransaction = customerTransaction;
            _accountTransaction = accountTransaction;
            _userTransaction = userTransaction;
            _jobOrderTransaction = jobOrderTransaction;
            _approvalHistoryTransaction = approvalHistoryTransaction;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRequest(int customerId, int accountId, bool isDraft, [FromBody] int[]? types)
        {
            bool isSuccess = false;
            Request request = new()
            {
                AccountId = accountId,
                Status = isDraft ? RequestStatus.Draft : RequestStatus.InProgress, 
                CustomerId = customerId
            };

            var createdRequest = await _requestTransaction.SaveUpdate(_authenticatedUserService.User.Id, request);
            if (createdRequest.Id > 0)
            {
                List<RequestDetail> requestDetails = new();
                if(types == null)
                {
                    var newConnectionInspectionType = await _inspectionTypeTransaction.GetNewRequestType();
                    RequestDetail requestDetail = new()
                    {
                        RequestId = createdRequest.Id,
                        RequestTypeId = newConnectionInspectionType.Id,
                    };
                    requestDetails.Add(requestDetail);
                }
                else
                {
                    foreach(var type in types)
                    {
                        RequestDetail requestDetail = new()
                        {
                            RequestId = createdRequest.Id,
                            RequestTypeId = type
                        };
                        requestDetails.Add(requestDetail);
                    }
                }

                var res = await _requestTransaction.SaveMulipleRequestDetail(_authenticatedUserService.User.Id, createdRequest.Id, requestDetails);
                List<JobOrder> jobOrders = new();
                if (res != null && res.Any() && !isDraft)
                {
                    foreach (var detail in res)
                    {
                        JobOrder jo = new()
                        {
                            RequestDetailId = detail.Id,
                            RequestId = createdRequest.Id,
                            Status = JobOrderStatus.Inspection
                        };

                        var jobOrder = await _jobOrderTransaction.SaveUpdate(_authenticatedUserService.User.Id, createdRequest.Id, jo);
                        if (jobOrder.Id > 0)
                        {
                            ApprovalHistory history = new()
                            {
                                Details = jobOrder.Status.GetDescription()
                            };
                            await _approvalHistoryTransaction.Save(_authenticatedUserService.User.Id, jobOrder.Id, history);
                            jobOrders.Add(jo);
                        }
                    }

                    if (jobOrders.Count > 0) isSuccess = true;
                }
                else isSuccess = res != null && res.Any();
            }

            return Ok(isSuccess);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitRequest(int requestId, int[] types)
        {
            List<JobOrder> jobOrders = new();
            foreach(int id in types)
            {
                JobOrder jo = new()
                {
                    RequestDetailId = id,
                    RequestId = requestId,
                    Status = JobOrderStatus.Inspection
                };

                var jobOrder = await _jobOrderTransaction.SaveUpdate(_authenticatedUserService.User.Id, requestId, jo);
                jobOrders.Add(jo);
            }

            if (jobOrders.Count <= 0) return Ok(false);

            return Ok(true);
        }

        [HttpGet]
        public async Task<IActionResult> GetRequests()
        {
            var requests = await _requestTransaction.GetRequests();
            
            if(requests == null || !requests.Any()) return NotFound();

            //var requestInfos = new[]
            //{
            //    new {
            //        Id = 0,
            //        ControlNumber = string.Empty,
            //        AccountId = 0,
            //        CustomerId = 0,
            //        Status = 0,
            //        CustomerName = string.Empty,
            //        AccountNumber = string.Empty,
            //        LoggedDate = DateTime.MinValue,
            //        LoggedBy = string.Empty
            //    }
            //}.Take(0).ToArray();

            List<dynamic> requestInfos = new();

            foreach (var request in requests)
            {
                Task<Customer> getCustomer = _customerTransaction.Get(request.CustomerId);
                Task<Account> getAccount = _accountTransaction.Get(request.AccountId);
                Task<User> getUser = _userTransaction.Get(request.CreatedBy);

                await Task.WhenAll(getCustomer, getAccount, getUser);

                var requestInfo = new
                {
                    Id = request.Id,
                    ControlNumber = request.ControlNumber,
                    AccountId = request.AccountId,
                    CustomerId = request.CustomerId,
                    Status = (int)request.Status,
                    CustomerName = getCustomer.Result.FullName,
                    AccountNumber = getAccount.Result.AccountNumber,
                    LoggedDate = request.DateCreated,
                    LoggedBy = getUser.Result.Name
                };

                requestInfos.Add(requestInfo);
            }

            return Ok(requestInfos.ToList());
        }

        [HttpGet]
        public async Task<IActionResult> GetInspectionType(int requestId = 0)
        {
            var getInspectionTypes = _inspectionTypeTransaction.GetTypes();
            var getRequestDetails = _requestTransaction.GetRequestDetailByRequestId(requestId);
            List<dynamic> data = new();

            if(requestId == 0)
            {
                await Task.WhenAll(getInspectionTypes);
                var types = getInspectionTypes.Result;
                foreach(var type in types)
                {
                    var inspectionType = new
                    {
                        Id = type.Id,
                        Name = type.Name,
                        IsRequiredAccount = type.IsRequiredAccount,
                        WithDetail = type.WithDetail,
                        IsActive = type.IsActive,
                        IsSelected = false
                    };
                    data.Add(inspectionType);
                }
            }
            else
            {
                await Task.WhenAll(getInspectionTypes, getRequestDetails);
                var types = getInspectionTypes.Result;
                var details = getRequestDetails.Result;

                foreach(var type in types)
                {
                    bool isSelected = details.Where(x => x.RequestTypeId == type.Id).Any();
                    var inspectionType = new
                    {
                        Id = type.Id,
                        Name = type.Name,
                        IsRequiredAccount = type.IsRequiredAccount,
                        WithDetail = type.WithDetail,
                        IsActive = type.IsActive,
                        IsSelected = isSelected
                    };
                    data.Add(inspectionType);
                }
            }

            if (data == null || !data.Any()) return NotFound();

            return Ok(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetRequest(int id)
        {
            var request = await _requestTransaction.Get(id);
            if(request == null) return NotFound();
            return Ok(request);
        }

        [HttpGet]
        public async Task<IActionResult> GetInspectionTypeById(int id)
        {
            var inspectionType = await _inspectionTypeTransaction.Get(id);
            if (inspectionType == null) return NotFound();
            return Ok(inspectionType);
        }

        [HttpGet]
        public async Task<IActionResult> GetRequestDetailById(int id, int requestId)
        {
            var detail = await _requestTransaction.GetRequestDetail(id, requestId);
            if (detail == null) return NotFound();
            return Ok(detail);
        }

    }
}
