using CRMBlazorServerRBS.CustomCodes;
using CRMBlazorServerRBS.Data;
using CRMBlazorServerRBS.Models;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radzen;
using System.Data;

// Позволяет Dapper сопоставлять колонки вида "row_version" со свойствами "RowVersion"
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// Allow large HTTP request bodies for the /upload/large endpoint.
// [DisableRequestSizeLimit] on the action removes the per-action cap,
// but Kestrel's server-wide limit must also be lifted (or set high enough).
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = null; // null = unlimited; set a long value if you want a hard cap
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddHubOptions(o =>
{
    o.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<CRMBlazorServerRBS.RadzenCRMService>();

builder.Services.AddDbContext<CRMBlazorServerRBS.Data.ApplicationIdentityDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("RadzenCRMConnection"));
});


builder.Services.AddHttpClient("CRMBlazorServerRBS").AddHeaderPropagation(o => o.Headers.Add("Cookie"));
builder.Services.AddHeaderPropagation(o => o.Headers.Add("Cookie"));
builder.Services.AddAuthentication()
    .AddNegotiate();          // Windows (Kerberos/NTLM) — дополнительная схема
builder.Services.AddAuthorization();
builder.Services.AddScoped<CRMBlazorServerRBS.SecurityService>();
builder.Services.AddScoped<CRMBlazorServerRBS.Services.MenuService>();
builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("RadzenCRMConnection"));
});

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationIdentityDbContext>().AddDefaultTokenProviders();


builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("RadzenCRMConnection")));

builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestInfoProvider, RequestInfoProvider>(); // Scoped for Session!!!
builder.Services.AddControllers();


builder.Services.AddScoped<AuthenticationStateProvider, CRMBlazorServerRBS.ApplicationAuthenticationStateProvider>();

builder.Services.AddScoped<UserContext>();

builder.Services.AddScoped<AppCircuitHandler>();
builder.Services.AddScoped<AuditService>();


var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseHeaderPropagation();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapControllers();
app.MapBlazorHub();
app.UseMiddleware<MyCustomMiddleware>();
app.MapFallbackToPage("/_Host");
//app.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>().Database.Migrate();
app.Run();