using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CRMBlazorServerRBS.CustomCodes
{
    public class AppCircuitHandler : CircuitHandler
    {
        private readonly IHttpContextAccessor _http;
        private readonly UserContext _user;

        public AppCircuitHandler(IHttpContextAccessor http, UserContext user)
        {
            _http = http;
            _user = user;
        }

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            var ctx = _http.HttpContext;

            _user.CircuitId = circuit.Id;
            _user.IP = ctx?.Connection.RemoteIpAddress?.ToString();
            _user.UserAgent = ctx?.Request.Headers["User-Agent"].ToString();

            // если есть авторизация
            _user.UserId = ctx?.User?.Identity?.Name;

            return Task.CompletedTask;
        }
    }
}
