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
using PureLedger.Services.Auth;
using ICacheInvalidationService = PureLedger.Services.ICacheInvalidationService;
using CacheInvalidationService = PureLedger.Services.CacheInvalidationService;
using PureLedger.Middleware;
using System.Linq;
using System.Text;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// PORT CONFIGURATION:
// - Development: Uses port from launchSettings.json (default: 5221)
// - Production: Uses ASPNETCORE_URLS environment variable (set to http://0.0.0.0:5104 in systemd service)
//   The systemd service will set: Environment=ASPNETCORE_URLS=http://0.0.0.0:5104
//   This allows Nginx to reverse proxy from port 80 to the backend on port 5104

// Note: Kestrel configuration is handled via ASPNETCORE_URLS environment variable in production
// No need to configure Kestrel explicitly - it will use the environment variable

// Add Memory Cache
builder.Services.AddMemoryCache();

// Configure Rate Limiting
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure Antiforgery for CSRF protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // JavaScript must read this cookie
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add services to the container.
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

// Configure CORS from appsettings.json - all settings must be in appsettings.json
var corsSection = builder.Configuration.GetSection("Cors");
var allowedOrigins = corsSection.GetSection("AllowedOrigins").Get<string[]>()!;
var allowCredentials = corsSection.GetValue<bool>("AllowCredentials");
var allowedHeaders = corsSection.GetSection("AllowedHeaders").Get<string[]>()!;
var allowedMethods = corsSection.GetSection("AllowedMethods").Get<string[]>()!;
var preflightMaxAge = corsSection.GetValue<int>("PreflightMaxAge");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        var builderPolicy = policy.WithOrigins(allowedOrigins);
        
        // Configure headers
        if (allowedHeaders.Contains("*"))
        {
            builderPolicy.AllowAnyHeader();
        }
        else
        {
            builderPolicy.WithHeaders(allowedHeaders);
        }
        
        // Configure methods
        if (allowedMethods.Contains("*"))
        {
            builderPolicy.AllowAnyMethod();
        }
        else
        {
            builderPolicy.WithMethods(allowedMethods);
        }
        
        // Configure credentials
        if (allowCredentials)
        {
            builderPolicy.AllowCredentials();
        }
        
        // Configure preflight cache
        builderPolicy.SetPreflightMaxAge(TimeSpan.FromSeconds(preflightMaxAge));
    });
    
    // Also add a default policy for development
    if (builder.Environment.IsDevelopment())
    {
        options.AddDefaultPolicy(policy =>
        {
            var builderPolicy = policy.WithOrigins(allowedOrigins);
            
            if (allowedHeaders.Contains("*"))
            {
                builderPolicy.AllowAnyHeader();
            }
            else
            {
                builderPolicy.WithHeaders(allowedHeaders);
            }
            
            if (allowedMethods.Contains("*"))
            {
                builderPolicy.AllowAnyMethod();
            }
            else
            {
                builderPolicy.WithMethods(allowedMethods);
            }
            
            if (allowCredentials)
            {
                builderPolicy.AllowCredentials();
            }
        });
    }
});

// Configure JWT Authentication - all settings must be in appsettings.json
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Get cookie settings from configuration
var accessTokenCookieName = builder.Configuration["Security:CookieSettings:AccessTokenCookieName"] ?? "accessToken";

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

    // Configure JWT to read from HttpOnly cookie as primary source
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First, try to get token from the cookie
            if (context.Request.Cookies.TryGetValue(accessTokenCookieName, out var cookieToken))
            {
                context.Token = cookieToken;
            }
            // If no cookie, fall back to Authorization header (handled by default)
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
builder.Services.AddScoped<PureLedger.Services.Auth.IAuthService, PureLedger.Services.Auth.AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISharedProcessingService, SharedProcessingService>();

// Register services
builder.Services.AddScoped<MetaDataValidationService>();
builder.Services.AddScoped<IMetaDataService, MetaDataService>();
builder.Services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();

// Register UnitOfWork - this will create all repositories internally with the same DbContext
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));


// Configure API options for global authorization filter
builder.Services.Configure<MvcOptions>(options =>
{
    // Create a policy that requires authenticated user
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
        
    // Global authorization filter applied to all API controllers
    // Individual controllers/actions can override with [AllowAnonymous]
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddScoped<IProjectCartageSettingsRepository, ProjectCartageSettingsRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Add global exception handling middleware - must be early in the pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

// Add CORS - must be before UseRouting and UseAuthentication to handle preflight requests
app.UseCors("AllowReactApp");

// Add Rate Limiting - must be after CORS to allow preflight requests
app.UseIpRateLimiting();

app.UseRouting();

// Add authentication - IMPORTANT: Must come before SecurityMiddleware
app.UseAuthentication(); // Validates JWT and populates context.User

app.UseSecurity();

app.UseAuthorization();

app.MapControllers();

app.Run();