using Microsoft.AspNetCore.Mvc;
using TMCWD.Administration;
using TMCWD.Application.Models;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Extensions;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class JobOrderController : Controller
    {

        private readonly AuthenticatedUserService _authService;
        private readonly JobOrderTransaction _jobOrderTransaction;
        private readonly RequestTransaction _requestTransaction;
        private readonly InspectionTypeTransaction _inspectionTypeTransaction;

        public JobOrderController(AuthenticatedUserService authService, 
            JobOrderTransaction jobOrderTransaction, 
            RequestTransaction requestTransaction,
            InspectionTypeTransaction inspectionTypeTransaction)
        {
            _authService = authService;
            _jobOrderTransaction = jobOrderTransaction;
            _requestTransaction = requestTransaction;
            _inspectionTypeTransaction = inspectionTypeTransaction;
        }

        public async Task<IActionResult> Index(int id, int requestId, int status)
        {
            FindingViewModel viewModel = new()
            {
                JobOrderId = id,
                RequestId = requestId,
                Status = (JobOrderStatus)status
            };
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetJobOrders(int requestId)
        {
            Task<List<JobOrder>> getJobOrders = _jobOrderTransaction.GetAll(requestId);
            Task<List<InspectionType>> getInspectionTypes = _inspectionTypeTransaction.GetTypes();

            await Task.WhenAll(getJobOrders, getInspectionTypes);

            var jobOrders = getJobOrders.Result;
            List<InspectionType> inspectionTypes = getInspectionTypes.Result;

            if (jobOrders == null || !jobOrders.Any()) return NoContent();

            List<dynamic> jos = new();

            foreach (var jobOrder in jobOrders)
            {

                var requestDetail = await _requestTransaction.GetRequestDetail(jobOrder.RequestDetailId, requestId);
                var inspectionType = inspectionTypes.Where(x => x.Id == requestDetail.RequestTypeId).FirstOrDefault();

                var jo = new
                {
                    Id = jobOrder.Id,
                    RequestId = jobOrder.RequestId,
                    RequestDetailId = jobOrder.RequestDetailId,
                    Status = jobOrder.Status,
                    StatusString = jobOrder.Status.GetStatusString(),
                    DateCreated = jobOrder.DateCreated,
                    Service = inspectionType?.Name ?? string.Empty,
                    ServiceTypeId = requestDetail.RequestTypeId
                };

                jos.Add(jo);
            }

            return Ok(jos);
        }

        [HttpGet]
        public async Task<IActionResult> GetJobOrder(int jobOrderId, int requestId)
        {
            var jobOrder = await _jobOrderTransaction.Get(jobOrderId, requestId);
            if (jobOrder == null) return NotFound();

            var request = await _requestTransaction.Get(requestId);
            var requestDetail = await _requestTransaction.GetRequestDetail(jobOrder.RequestDetailId, requestId);
            var inspectionType = await _inspectionTypeTransaction.Get(requestDetail.RequestTypeId);

            return Ok(new { OrderNumber = jobOrder.JobOrderNumber, ServiceType = inspectionType.Name, DateLogged = request.DateCreated, Status = jobOrder.Status.GetStatusString(), StatusValue = (int)jobOrder.Status });
        }

        [HttpPost]
        public async Task<IActionResult> MoveToNextStatus(int jobOrderId, int requestId)
        {
            var jobOrder = await _jobOrderTransaction.Get(jobOrderId, requestId);
            if(jobOrder == null) return NotFound();

            jobOrder.Status = jobOrder.Status.GetNext();

            var updatedJobOrder = await _jobOrderTransaction.SaveUpdate(_authService.User.Id, requestId, jobOrder);

            if(updatedJobOrder == null) return NoContent();

            return Ok(jobOrder);
        }

    }
}
