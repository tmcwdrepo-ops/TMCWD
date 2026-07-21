using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IWorkflowService
    {
        Task<Workflow?> Get(int id);
        Task<List<Workflow>> GetAll();
        Task<Workflow> SaveUpdate(int userId, Workflow workflow);
        Task<bool> Delete(Workflow workflow);
    }
}
