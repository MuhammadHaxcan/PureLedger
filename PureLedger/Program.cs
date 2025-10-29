using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Repositories;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services;
using PureLedger.Services.Interface;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Configure authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        
        // Add this to prevent redirect loops
        options.Cookie.Name = "PureLedgerAuth"; 
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                // Check if already on login page to prevent loops
                if (context.Request.Path.StartsWithSegments("/Account/Login"))
                {
                    // Don't redirect if already on login page
                    context.Response.StatusCode = 200;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

// Register LedgerDbContext directly with connection string from configuration
builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("LedgerDb")));

builder.Services.AddScoped<JournalEntryService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register services
builder.Services.AddScoped<MetaDataValidationService>();
builder.Services.AddScoped<IMetaDataService, MetaDataService>();

// Register UnitOfWork - this will create all repositories internally with the same DbContext
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{   
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure MVC options for global authorization filter
builder.Services.Configure<MvcOptions>(options =>
{
    // Create a policy that allows anonymous access
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
        
    // Global authorization filter applied to all controllers
    // Individual controllers/actions can override with [AllowAnonymous]
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddScoped<IProjectCartageSettingsRepository, ProjectCartageSettingsRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add session and authentication - IMPORTANT: order matters here
app.UseSession();
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// REMOVED: Custom authorization middleware - using attribute-based authorization instead

// Simplified route pattern
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Entry}/{id?}");
    
// Add redirect for root URL to provide a better landing experience
app.MapGet("/", context => {
    // Don't redirect if already authenticated
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Home/Entry");
        return Task.CompletedTask;
    }
    
    // Simple redirect without ReturnUrl to prevent loops
    context.Response.Redirect("/Account/Login");
    return Task.CompletedTask;
}).AllowAnonymous();

app.Run();