namespace CRMBlazorServerRBS.CustomCodes
{
    public class AuditService
    {
        private readonly UserContext _user;

        public AuditService(UserContext user)
        {
            _user = user;
        }

        public void Log(string action, string details = null)
        {
            var log = new
            {
                Time = DateTime.UtcNow,
                User = _user.UserId,
                IP = _user.IP,
                Action = action,
                Details = details
            };

            // сюда можно:
            // - в БД
            // - в файл
            // - в Serilog

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(log));
        }
    }
}
