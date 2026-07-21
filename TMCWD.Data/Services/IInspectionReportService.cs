using System.Runtime.CompilerServices;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IInspectionReportService
    {
        Task<InspectionReport> SaveUpdate(int userId, InspectionReport report);
        Task<InspectionReport> Get(int id);
        Task<List<InspectionReport>> GetByRequestId(int requestId);
    }
}
