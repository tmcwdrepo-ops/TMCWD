using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class WorkflowService : IWorkflowService
    {

        private readonly UserDbContext _context;

        public WorkflowService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<bool> Delete(Workflow workflow)
        {
            _context.Workflows.Remove(workflow);
            int res = await _context.SaveChangesAsync();
            return res > 0;
        }

        public async Task<Workflow?> Get(int id)
        {
            var workflow = await _context.Workflows.Where(x => x.Id == id).FirstOrDefaultAsync();
            return workflow;
        }

        public async Task<List<Workflow>> GetAll()
        {
            var workflows = _context.Workflows;
            return await workflows.ToListAsync();
        }

        public async Task<Workflow> SaveUpdate(int userId, Workflow workflow)
        {
            workflow.DateUpdated = DateTime.Now;
            if (workflow.Id > 0)
            {
                workflow.UpdatedBy = userId;
                _context.Workflows.Update(workflow);
            }
            else
            {
                workflow.DateCreated = DateTime.Now;
                workflow.CreatedBy = userId;
            }

            await _context.SaveChangesAsync();
            return workflow;
        }

    }

}
