namespace CRMBlazorServerRBS.CustomCodes
{
    public class MyCustomMiddleware
    {
        private readonly RequestDelegate _next;

        public MyCustomMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            

            var ip = context.Connection.RemoteIpAddress?.ToString();
            Console.WriteLine($"Client IP: {ip}");

            // Передаём управление дальше по пайплайну
            await _next(context);
        }
    }

}
