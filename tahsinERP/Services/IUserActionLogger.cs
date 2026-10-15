namespace tahsinERP.Services
{
    /// <summary>
    /// Interface for logging user actions to USERLOGS table
    /// </summary>
    public interface IUserActionLogger
    {
        void Log(string controller, string action, string details = null);
    }
}

