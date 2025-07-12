using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Services;
using PureLedger.Repositories;
using PureLedger.Repositories.Interfaces;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<JournalEntryValidationService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IJournalVoucherRepository, JournalVoucherRepository>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<IDeliveryChallanService, DeliveryChallanService>();

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

// Add LedgerDbContext with SQL Server and Windows Authentication
builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LedgerDb")));

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Entry}/{id?}");

app.Run();