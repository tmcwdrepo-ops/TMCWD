using Microsoft.AspNetCore.Mvc;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Extensions;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class RequestFileController : Controller
    {
        private readonly RequestFileTransaction _requestFileTransaction;
        private readonly AuthenticatedUserService _authenticatedUserService;

        public RequestFileController(RequestFileTransaction requestFileTransaction, AuthenticatedUserService authUserService)
        {
            _requestFileTransaction = requestFileTransaction;
            _authenticatedUserService = authUserService;
        }

        [HttpPost]
        public async Task<IActionResult> Save(int requestFileType, int jobOrderId, List<IFormFile> files)
        {
            if (!files.Any()) return NoContent();
            List<RequestFile> requestFiles = new();

            foreach(var file in files)
            {
                RequestFile requestFile = new()
                {
                    DateCreated = DateTime.Now,
                    DateUpdated = DateTime.Now,
                    JobOrderId = jobOrderId,
                    CreatedBy = _authenticatedUserService.User.Id,
                    UpdatedBy = _authenticatedUserService.User.Id,
                    RequestFileType = (RequestFileType)requestFileType,
                    Type = Path.GetExtension(file.FileName.ToLower()).Replace(".", "").GetFileTypeFromExtenstion()
                };
                requestFiles.Add(requestFile);
            }

            List<RequestFile> insertedFiles = await _requestFileTransaction.SaveRange(requestFiles);

            return Ok(insertedFiles);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(int jobOrderId)
        {
            var files = await _requestFileTransaction.GetAll(jobOrderId);
            if (files == null || !files.Any()) return NoContent();
            return Ok(files);
        }

    }
}
