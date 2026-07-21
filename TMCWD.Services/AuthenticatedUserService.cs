using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator;

namespace TMCWD.Services
{
    public class AuthenticatedUserService
    {

        #region fields

        private User _user;

        #endregion


        #region constructors

        public AuthenticatedUserService() 
        { 
            _user = new User();
        }

        #endregion

        #region properties

        public User User
        {
            get
            {
                return _user;
            }
        }

        #endregion

        #region methods

        public void SetUser(User user)
        {
            this._user = user;
        }

        #endregion

    }
}
