using TMCWD.Data.Entities;
namespace TMCWD.Data.Services
{
    public interface IUserService
    {
        Task<User> SaveUpdate(int userId, User user);
        Task<User?> Get(int id);
        Task<User?> GetByName(string name);
        Task<User?> GetByEmail(string email);
        Task<IEnumerable<User>?> GetUsers();
        Task<IEnumerable<User>?> SearchUser(string searchString);
        Task<IEnumerable<User>> GetUsersByRole(int roleId);
    }
}
