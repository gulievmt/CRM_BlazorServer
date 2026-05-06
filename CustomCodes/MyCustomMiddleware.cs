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
            Console.ForegroundColor = ConsoleColor.Yellow;
            var ip = context.Connection.RemoteIpAddress?.ToString() + "   id = "+ context.Connection.Id;
            Console.WriteLine($"Client IP: {ip}");

            // Передаём управление дальше по пайплайну
            await _next(context);
        }
    }

}
