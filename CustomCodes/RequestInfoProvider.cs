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

            return (_httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown" )+"  "+
                _httpContextAccessor.HttpContext?.Session?.GetString("Begin_time") + "  " +
                DateTime.Now.Ticks.ToString();
        }
    }

}
