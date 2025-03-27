using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Gold_Billing_Web_App.Models;
using Gold_Billing_Web_App.Session;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Configuration
var configuration = builder.Configuration;
builder.Services.AddSingleton<IConfiguration>(configuration);

// Add DbContext with null check for connection string
var connectionString = configuration.GetConnectionString("ConnectionString");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'ConnectionString' not found in configuration.");
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/UserAccount/Login";
        options.LogoutPath = "/UserAccount/Logout";
        options.AccessDeniedPath = "/UserAccount/Login";
    });

// Add global filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(typeof(LoginCheckAccess));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection(); // Only enable HTTPS redirection in non-Development environments
}

app.UseStaticFiles();
app.UseRouting();

// Ensure session and authentication are before authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=UserAccount}/{action=Login}/{id?}");

app.Run();