using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class RequestFileService : IRequestFileService
    {

        private readonly UserDbContext _context;

        public RequestFileService(UserDbContext context) { _context = context; }

        public async Task<RequestFile?> Get(int id)
        {
            var requestFile = await _context.Files.Where(x => x.Id == id).FirstOrDefaultAsync();
            return requestFile;
        }

        public async Task<List<RequestFile>> GetAll(int jobOrderId)
        {
            var requestFiles = _context.Files.Where(x => x.JobOrderId == jobOrderId);
            return await requestFiles.ToListAsync();
        }

        public async Task<List<RequestFile>> SaveRange(RequestFile[] files)
        {
            _context.Files.AddRange(files);
            await _context.SaveChangesAsync();
            return files.ToList();
        }

        public async Task<RequestFile?> SaveUpdate(int userId, int jobOrderId, RequestFile file)
        {
            file.DateUpdate = DateTime.Now;
            if(file.Id > 0)
            {
                file.UpdatedBy = userId;
                _context.Files.Update(file);
            }
            else
            {
                file.CreatedBy = userId;
                file.DateCreated = DateTime.Now;
                _context.Files.Add(file);
            }

            await _context.SaveChangesAsync();

            return file;

        }
    }
}
