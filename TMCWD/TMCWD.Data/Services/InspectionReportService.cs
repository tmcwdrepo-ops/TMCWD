using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class InspectionReportService : IInspectionReportService
    {
        private readonly UserDbContext _dbContext;

        public InspectionReportService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<InspectionReport> Get(int id)
        {

            var inspectionReport = await _dbContext.InspectionReports.Where(x => x.Id == id).FirstOrDefaultAsync();
            return inspectionReport ?? new InspectionReport();
        }

        public async Task<List<InspectionReport>> GetByRequestId(int requestId)
        {
            var inspectionReports = await _dbContext.InspectionReports.Where(x => x.RequestId == requestId).ToListAsync();
            return inspectionReports ?? new List<InspectionReport>();
        }

        public async Task<InspectionReport> SaveUpdate(int userId, InspectionReport report)
        {
            if(report.Id > 0)
            {
                report.UpdatedBy = userId;
                report.DateUpdated = DateTime.Now;
                _dbContext.InspectionReports.Update(report);
            }
            else
            {
                report.CreatedBy = userId;
                report.DateCreated = DateTime.Now;
                _dbContext.InspectionReports.Add(report);
            }

            await _dbContext.SaveChangesAsync();
            return report;

        }
    }
}
