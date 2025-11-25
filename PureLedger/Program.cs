using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Mapping;
using PureLedger.Repositories;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services;
using PureLedger.Services.Interface;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to handle both camelCase and PascalCase
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization to handle both camelCase and PascalCase
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Handle circular references
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Customize the response for invalid model state
        options.InvalidModelStateResponseFactory = context =>
        {
            // Remove navigation property errors (they're not sent in JSON, only IDs are)
            var keysToRemove = context.ModelState.Keys
                .Where(k => k == "Project" || k == "AccountType" || k == "Accounts" || 
                           k == "Category.Project" || k == "Category.AccountType" || k == "Category.Accounts")
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                context.ModelState.Remove(key);
            }

            // Get remaining errors
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage))
                .ToList();
            
            return new BadRequestObjectResult(new { success = false, errors = errors });
        };
    }); // For API controllers
builder.Services.AddHttpContextAccessor();

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PureLedger";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PureLedger";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.Cookie.Name = "PureLedgerAuth";
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/Account/Login"))
            {
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

// Register services
builder.Services.AddScoped<JournalEntryService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Register services
builder.Services.AddScoped<MetaDataValidationService>();
builder.Services.AddScoped<IMetaDataService, MetaDataService>();

// Register UnitOfWork - this will create all repositories internally with the same DbContext
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

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

// Add CORS - must be before UseAuthentication
app.UseCors("AllowReactApp");

// Add session and authentication - IMPORTANT: order matters here
app.UseSession();
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// REMOVED: Custom authorization middleware - using attribute-based authorization instead

// API routes
app.MapControllers();

// MVC routes (for backward compatibility if needed)
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