using TMCWD.Data.Context;
using TMCWD.Model.Administrator;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Test
{
    public class TestUser
    {

        public TestUser() { }

        public User GetByEmail(string email)
        {
            User user = new();

            try
            {
                using (var dbContext = new UserDbContext())
                {
                    var userEnt = dbContext.Users.Where(x => x.Email == email).SingleOrDefault();
                    if (userEnt == null) throw new Exception($"User with email {email} is not found.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ErrorModule.Data, ErrorType.Error, ex.Message);
            }

            return user;
        }

    }
}
