namespace CRMBlazorServerRBS.CustomCodes
{
    public interface IRequestInfoProvider
    {
        string GetClientIp();
    }

    public class RequestInfoProvider : IRequestInfoProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
           
        public RequestInfoProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private bool initialized = false;
        public string GetClientIp()
        {
            if(_httpContextAccessor.HttpContext?.Session != null  && !initialized)
            {
                initialized = true;
                _httpContextAccessor.HttpContext?.Session.SetString("Begin_time",  DateTime.Now.ToLongTimeString());
            }

            return "IP address: "+(_httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown" )+"   Begin time: "+
                _httpContextAccessor.HttpContext?.Session?.GetString("Begin_time") + "   CurrentTime:" +
                DateTime.Now.ToLongTimeString();
        }
    }

}
