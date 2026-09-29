using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class BatchPaymentUploadService : IBatchPaymentUploadService
    {
        private readonly UserDbContext _context;

        public BatchPaymentUploadService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<List<BatchPaymentUpload>> GetByDateRange(DateTime from, DateTime to) =>
            await _context.BatchPaymentUploads
                .AsNoTracking()
                .Where(b => b.DateCreated.Date >= from.Date && b.DateCreated.Date <= to.Date)
                .OrderByDescending(b => b.DateCreated)
                .ToListAsync();

        public async Task<BatchPaymentUpload?> Get(long id) =>
            await _context.BatchPaymentUploads
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

        public async Task<BatchPaymentUpload> Save(int userId, BatchPaymentUpload upload)
        {
            upload.CreatedBy    = userId;
            upload.DateCreated  = DateTime.Now;
            _context.BatchPaymentUploads.Add(upload);
            await _context.SaveChangesAsync();
            return upload;
        }
    }
}
