using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TMCWD.Application.Models;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Extensions;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class FindingController : Controller
    {

        private readonly FindingTransaction _findingTransaction;
        private readonly AuthenticatedUserService _authenticatedUser;
        private readonly RequestFileTransaction _requestFileTransaction;

        public FindingController(FindingTransaction findingTransaction, AuthenticatedUserService authenticatedUser, RequestFileTransaction requestFileTransaction)
        {
            _findingTransaction = findingTransaction;
            _authenticatedUser = authenticatedUser;
            _requestFileTransaction = requestFileTransaction;
        }


        [HttpPost]
        public async Task<IActionResult> SaveFinding(int jobOrderId, [FromBody] Finding finding)
        {
            if (finding == null || string.IsNullOrEmpty(finding.Detail.Trim())) return NoContent();

            var savedFinding = await _findingTransaction.SaveUpdate(_authenticatedUser.User.Id, jobOrderId, finding);
            if (savedFinding == null || savedFinding.Id <= 0) return NoContent();
            return Ok(savedFinding);
        }

        [HttpPost]
        public async Task<IActionResult> SaveFindingFile(int jobOrderId, List<IFormFile> files)
        {
            if (files == null || !files.Any()) return Ok(false);

#if DEBUG
            var parentUploadPath = $"../TMCWD.Application/wwwroot";
#else
            var parentUploadPath = $"../";
#endif

            if (!Directory.Exists(parentUploadPath))
            {
                Directory.CreateDirectory(Path.GetFullPath(parentUploadPath));
            }

            string currentPath = $"Uploads/{DateTime.Now.ToString("yyyyMMdd")}";
            var currentUploadPath = Path.Combine(parentUploadPath, currentPath);

            if (!Directory.Exists(currentUploadPath))
            {
                Directory.CreateDirectory(currentUploadPath);

            }
            List<RequestFile> requestFiles = new();

            foreach (var file in files)
            {
                string extenstion = Path.GetExtension(file.FileName).Replace(".", "");
                RequestFile reqFile = new()
                {
                    JobOrderId = jobOrderId,
                    OriginalFilename = file.FileName,
                    PhysicalFilename = $"{Guid.NewGuid().ToString().ToUpper().Replace("-", "")}.{extenstion}",
                    RequestFileType = RequestFileType.Finding,
                    Path = $"/{currentPath}",
                    CreatedBy = _authenticatedUser.User.Id,
                    Type = extenstion.GetFileTypeFromExtenstion(),
                    Size = file.Length,
                    DateCreated = DateTime.Now
                    
                };

                requestFiles.Add(reqFile);

                using (Stream stream = new FileStream(Path.Combine(Path.GetFullPath(currentUploadPath), reqFile.PhysicalFilename), FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(stream);
                }

            }
            List<RequestFile> savedFiles = await _requestFileTransaction.SaveRange(requestFiles);

            return Ok(savedFiles != null && !savedFiles.Any());
        }

        [HttpGet]
        public async Task<IActionResult> GetFindings(int jobOrderId)
        {
            var finding = await _findingTransaction.GetAll(jobOrderId);
            if (finding == null || !finding.Any()) return NotFound();

            var requestFile = await _requestFileTransaction.GetAll(jobOrderId);

            return Ok(new { Findings = finding, Files = requestFile ?? new List<RequestFile>() });
        }

    }
}
