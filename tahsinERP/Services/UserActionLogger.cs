using tahsinERP.Controllers;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for logging user actions to USERLOGS table.
    /// LogHelper bilan bir xil yo'l (navbatli yozuv, mijoz IP'si) — avval server IP'sini yozardi.
    /// </summary>
    public class UserActionLogger : IUserActionLogger
    {
        private readonly string _userEmail;

        public UserActionLogger(string userEmail)
        {
            _userEmail = userEmail;
        }

        public void Log(string controller, string action, string details = null)
        {
            LogHelper.LogToDatabase(_userEmail, controller, action, details);
        }
    }
}
