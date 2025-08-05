using PureLedger.Data;
using PureLedger.Services;
using PureLedger.Repositories;
using PureLedger.Repositories.Interfaces;
using PureLedger.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Register DbContext factory
builder.Services.AddSingleton<SubrouteDbContextFactory>();

// Register all services
builder.Services.AddScoped<JournalEntryValidationService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IJournalVoucherRepository, JournalVoucherRepository>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<JournalEntryService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();
builder.Services.AddScoped<IReportingService, ReportingService>();

// Register repositories
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IAccountGroupRepository, AccountGroupRepository>();
builder.Services.AddScoped<IAccountDataRepository, AccountDataRepository>();
builder.Services.AddScoped<IProductDataRepository, ProductDataRepository>();
builder.Services.AddScoped<IAccountTypeRepository, AccountTypeRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IDeliveryChallanVoucherRepository, DeliveryChallanVoucherRepository>();
builder.Services.AddScoped<IDeliveryChallanEntryRepository, DeliveryChallanEntryRepository>();

// Register services
builder.Services.AddScoped<MetaDataValidationService>();
builder.Services.AddScoped<IMetaDataService, MetaDataService>();

// Register LedgerDbContext per request, using the subroute from HttpContext.Items
builder.Services.AddScoped<LedgerDbContext>(provider =>
{
    var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
    var factory = provider.GetRequiredService<SubrouteDbContextFactory>();
    var subroute = httpContextAccessor.HttpContext?.Items["Subroute"]?.ToString() ?? "Default";
    return factory.CreateDbContext(subroute);
});

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

app.UseSubrouteMiddleware();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{prefix}/{controller=Home}/{action=Entry}/{id?}",
    constraints: new { prefix = "route1|route2" });
app.Run();