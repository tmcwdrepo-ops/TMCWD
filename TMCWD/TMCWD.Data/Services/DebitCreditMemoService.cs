using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class DebitCreditMemoService : IDebitCreditMemoService
    {
        private readonly UserDbContext _context;

        public DebitCreditMemoService(UserDbContext context)
        {
            _context = context;
        }

        public Task<DebitCreditMemo?> Get(long id) =>
            _context.DebitCreditMemos.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

        public Task<List<DebitCreditMemo>> GetAll() =>
            _context.DebitCreditMemos.AsNoTracking().OrderByDescending(m => m.DateCreated).ToListAsync();

        public async Task<DebitCreditMemo> Save(int userId, DebitCreditMemo memo)
        {
            memo.CreatedBy = userId;
            memo.UpdatedBy = userId;
            memo.DateCreated = DateTime.UtcNow;
            memo.DateUpdated = memo.DateCreated;
            memo.Status = "Pending";
            _context.DebitCreditMemos.Add(memo);
            await _context.SaveChangesAsync();
            return memo;
        }
    }
}
