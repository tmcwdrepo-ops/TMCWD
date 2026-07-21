using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Utility.Encryption;
namespace TMCWD.Data.Services
{
    public class UserService : IUserService
    {
        private readonly UserDbContext _dbContext;

        public UserService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<User?> Get(int id)
        {
            var user = await _dbContext.Users.Where(x => x.Id == id).FirstOrDefaultAsync();
            return user;
        }

        public async Task<User?> GetByEmail(string email)
        {
            var user = await _dbContext.Users.Where(x => x.Email.ToLower() == email.ToLower()).FirstOrDefaultAsync();
            return user;
        }

        public async Task<User?> GetByName(string name)
        {
            var user = await _dbContext.Users.Where(x => x.Name.ToLower() == name.ToLower()).FirstOrDefaultAsync();
            return user;
        }

        public async Task<IEnumerable<User>?> GetUsers()
        {
            var users = _dbContext.Users;
            return await users.ToListAsync();
        }

        public async Task<User> SaveUpdate(int userId, User user)
        {
            if(user.Id > 0)
            {
                user.UpdatedBy = userId;
                user.DateUpdated = DateTime.Now;
                _dbContext.Users.Update(user);
            }
            else
            {
                user.CreatedBy = userId;
                user.DateCreated = DateTime.Now;
                _dbContext.Users.Add(user);
            }

            await _dbContext.SaveChangesAsync();
            return user;
        }

        public async Task<IEnumerable<User>?> SearchUser(string searchString)
        {
            var users = _dbContext.Users.Where(x => x.Name == searchString || x.Email == searchString);
            return await users.ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersByRole(int roleId)
        {
            var users = await _dbContext.Users.Where(x => x.Role == roleId).ToListAsync();
            return users;
        }
    }
}
