namespace TMCWD.Application.Models
{
    public class LoginViewModel
    {
        #region constructors
        public LoginViewModel() { }
        #endregion

        #region properties
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        #endregion
    }
}
