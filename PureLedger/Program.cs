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
using PureLedger.Middleware;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on all IPs
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Listen on all IPs, not just localhost
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

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:5173", "http://localhost:5174", "http://localhost:3000", "https://localhost:5173", "https://localhost:5174", "http://72.61.118.244:5000", "https://72.61.118.244:5000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromSeconds(3600));
    });
    
    // Also add a default policy for development
    if (builder.Environment.IsDevelopment())
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    }
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

app.UseRouting();

// Add CORS - must be before UseAuthentication
app.UseCors("AllowReactApp");

// Add authentication - IMPORTANT: order matters here
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// REMOVED: Custom authorization middleware - using attribute-based authorization instead

// API routes
app.MapControllers();

app.Run();