using System;
using System.Data.Entity.Infrastructure;
using tahsinERP.Controllers;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// Service for logging user actions to USERLOGS table
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
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    USERLOG log = new USERLOG
                    {
                        ControllerName = controller,
                        ActionName = action,
                        DateTime = DateTime.Now,
                        UserID = UserHelper.GetUserId(_userEmail),
                        IP = NetworkHelper.GetIpAddress(),
                        MacAddr = NetworkHelper.GetMacAddress(NetworkHelper.GetIpAddress())
                    };

                    db.USERLOGS.Add(log);
                    db.SaveChanges();
                }
            }
            catch (RetryLimitExceededException)
            {
                // Log silently - don't break user flow
            }
            catch (Exception)
            {
                // Log silently - don't break user flow
            }
        }
    }
}

