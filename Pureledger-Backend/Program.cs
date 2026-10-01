using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Repositories;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.Services.Auth;
using ICacheInvalidationService = PureLedger.Services.ICacheInvalidationService;
using CacheInvalidationService = PureLedger.Services.CacheInvalidationService;
using PureLedger.Middleware;
using PureLedger.Filters;
using System.Linq;
using System.Text;
using AspNetCoreRateLimit;
using Serilog;
using Serilog.Events;
using PureLedger.Services.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "PureLedger")
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/pureledger-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}");
});

// Configure Forwarded Headers for nginx reverse proxy (required for HTTPS)
builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// Configure Antiforgery for CSRF protection (HTTP compatible)
builder.Services.AddAntiforgery(options => {
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // Must be false so JavaScript can read and send it
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Allow HTTP
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/"; // Use root path to ensure cookie is always sent
    options.Cookie.IsEssential = true; // Ensure cookie is always set even without consent
    // Domain is intentionally not set - it will default to the request host
    // This works correctly with nginx proxy as long as Host header is forwarded
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        // Configure JSON serialization to handle both camelCase and PascalCase
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Handle circular references
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options => {
        // Customize the response for invalid model state
        options.InvalidModelStateResponseFactory = context => {
            // Remove navigation property errors (they're not sent in JSON, only IDs are)
            var keysToRemove = context.ModelState.Keys
                .Where(k => k == "Project" || k == "AccountType" || k == "Accounts" ||
                           k == "Category.Project" || k == "Category.AccountType" || k == "Category.Accounts")
                .ToList();

            foreach (var key in keysToRemove) {
                context.ModelState.Remove(key);
            }

            // Get remaining errors
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();

            var responseFactory = context.HttpContext.RequestServices.GetRequiredService<IApiErrorResponseFactory>();
            var response = responseFactory.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "Please fix the highlighted validation errors and try again.",
                errors,
                "VALIDATION_FAILED");

            return new BadRequestObjectResult(response);
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

builder.Services.AddCors(options => {
    options.AddPolicy("AllowReactApp", policy => {
        var builderPolicy = policy.WithOrigins(allowedOrigins);

        // Configure headers
        if (allowedHeaders.Contains("*")) {
            builderPolicy.AllowAnyHeader();
        } else {
            builderPolicy.WithHeaders(allowedHeaders);
        }

        // Configure methods
        if (allowedMethods.Contains("*")) {
            builderPolicy.AllowAnyMethod();
        } else {
            builderPolicy.WithMethods(allowedMethods);
        }

        // Configure credentials
        if (allowCredentials) {
            builderPolicy.AllowCredentials();
        }

        // Configure preflight cache
        builderPolicy.SetPreflightMaxAge(TimeSpan.FromSeconds(preflightMaxAge));
        builderPolicy.WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
    });

    // Also add a default policy for development
    if (builder.Environment.IsDevelopment()) {
        options.AddDefaultPolicy(policy => {
            var builderPolicy = policy.WithOrigins(allowedOrigins);

            if (allowedHeaders.Contains("*")) {
                builderPolicy.AllowAnyHeader();
            } else {
                builderPolicy.WithHeaders(allowedHeaders);
            }

            if (allowedMethods.Contains("*")) {
                builderPolicy.AllowAnyMethod();
            } else {
                builderPolicy.WithMethods(allowedMethods);
            }

            if (allowCredentials) {
                builderPolicy.AllowCredentials();
            }

            builderPolicy.WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
        });
    }
});

// Configure JWT Authentication - all settings must be in appsettings.json
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Get cookie settings from configuration
var accessTokenCookieName = builder.Configuration["Security:CookieSettings:AccessTokenCookieName"] ?? "accessToken";

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters {
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
    options.Events = new JwtBearerEvents {
        OnMessageReceived = context => {
            // First, try to get token from the cookie
            if (context.Request.Cookies.TryGetValue(accessTokenCookieName, out var cookieToken)) {
                context.Token = cookieToken;
            }
            // If no cookie, fall back to Authorization header (handled by default)
            return Task.CompletedTask;
        }
    };
});

// Register LedgerDbContext directly with connection string from configuration
builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("LedgerDb")),
    optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<LedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("LedgerDb")));

// Register OldLedgerDbContext for legacy MariaDB database
builder.Services.AddDbContext<OldLedgerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("OldLedgerDb");
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
    {
        mySqlOptions.EnableRetryOnFailure(3);
    });
});

// Register OldLedgerDb2Context for second legacy MariaDB database
builder.Services.AddDbContext<OldLedgerDb2Context>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("OldLedgerDb2");
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
    {
        mySqlOptions.EnableRetryOnFailure(3);
    });
});

// Register services
builder.Services.AddScoped<JournalEntryService>();
builder.Services.AddScoped<JournalEntryValidationService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IOldReportingService, OldReportingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISharedProcessingService, SharedProcessingService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IApplicationLogService, ApplicationLogService>();
builder.Services.AddScoped<IApiErrorResponseFactory, ApiErrorResponseFactory>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IApprovalGuardService, ApprovalGuardService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();
builder.Services.AddScoped<IPaymentReceiptClassifier, PaymentReceiptClassifier>();
builder.Services.AddSingleton<IPaymentReceiptApprovalCheckpoint, NoOpPaymentReceiptApprovalCheckpoint>();
builder.Services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();
builder.Services.AddScoped<ApiErrorResultFilter>();

// Register services
builder.Services.AddScoped<MetaDataValidationService>();
builder.Services.AddScoped<IMetaDataService, MetaDataService>();
builder.Services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();

// Register UnitOfWork - this will create all repositories internally with the same DbContext
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Configure API options for global authorization filter
builder.Services.Configure<MvcOptions>(options => {
    // Create a policy that requires authenticated user
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Global authorization filter applied to all API controllers
    // Individual controllers/actions can override with [AllowAnonymous]
    options.Filters.Add(new AuthorizeFilter(policy));

    // Add XSRF token validation for authenticated state-changing requests
    options.Filters.Add(new ValidateXsrfTokenAttribute());
    options.Filters.AddService<ApiErrorResultFilter>();
});

builder.Services.AddScoped<IProjectCartageSettingsRepository, ProjectCartageSettingsRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Add forwarded headers - MUST be first to properly detect HTTPS from nginx
app.UseForwardedHeaders();

app.UseMiddleware<CorrelationIdMiddleware>();

// Add global exception handling middleware - must be early in the pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseMiddleware<RequestBodyCaptureMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) =>
        ex != null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier);
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        diagnosticContext.Set("Username", httpContext.User.Identity?.Name);
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

if (!app.Environment.IsDevelopment()) {
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
