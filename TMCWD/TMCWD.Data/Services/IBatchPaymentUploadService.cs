using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IBatchPaymentUploadService
    {
        Task<List<BatchPaymentUpload>> GetByDateRange(DateTime from, DateTime to);
        Task<BatchPaymentUpload?> Get(long id);
        Task<BatchPaymentUpload> Save(int userId, BatchPaymentUpload upload);
    }
}
