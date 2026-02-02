using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models.Master;
using PureLedger.Services.Tenant;
using System.Security.Claims;

namespace PureLedger.Controllers.Api
{
    [ApiController]
    [Route("api/system/tenants")]
    [Authorize(Roles = "SystemAdmin")]
    public class TenantsController : ControllerBase
    {
        private readonly MasterDbContext _masterDb;
        private readonly ITenantProvisioningService _provisioningService;
        private readonly ITenantMigrationService _migrationService;
        private readonly ITenantDecommissioningService _decommissioningService;
        private readonly ITenantCacheService _cacheService;
        private readonly ILogger<TenantsController> _logger;

        public TenantsController(
            MasterDbContext masterDb,
            ITenantProvisioningService provisioningService,
            ITenantMigrationService migrationService,
            ITenantDecommissioningService decommissioningService,
            ITenantCacheService cacheService,
            ILogger<TenantsController> logger)
        {
            _masterDb = masterDb;
            _provisioningService = provisioningService;
            _migrationService = migrationService;
            _decommissioningService = decommissioningService;
            _cacheService = cacheService;
            _logger = logger;
        }

        private string GetCurrentAdmin() => User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        /// <summary>
        /// Get all tenants.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenants = await _masterDb.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Slug,
                    t.SchemaName,
                    t.IsActive,
                    t.CreatedAt,
                    t.ActivatedAt,
                    t.DeactivatedAt,
                    t.Notes
                })
                .ToListAsync();

            return Ok(new { success = true, tenants });
        }

        /// <summary>
        /// Get tenant by ID.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tenant = await _masterDb.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound(new { success = false, message = "Tenant not found" });
            }

            // Get recent audit logs
            var auditLogs = await _masterDb.TenantAuditLogs
                .Where(l => l.TenantId == id)
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Select(l => new
                {
                    l.Id,
                    l.Operation,
                    l.PerformedBy,
                    l.IsSuccess,
                    l.Details,
                    l.ErrorMessage,
                    l.Timestamp
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                tenant = new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    tenant.SchemaName,
                    tenant.IsActive,
                    tenant.CreatedAt,
                    tenant.ActivatedAt,
                    tenant.DeactivatedAt,
                    tenant.Notes
                },
                auditLogs
            });
        }

        /// <summary>
        /// Create a new tenant.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input" });
            }

            // Validate slug format
            if (!TenantSchemaValidator.TryValidateSlug(request.Slug, out var slugError))
            {
                return BadRequest(new { success = false, message = slugError });
            }

            // Check if slug already exists
            if (await _provisioningService.SlugExistsAsync(request.Slug))
            {
                return BadRequest(new { success = false, message = $"A tenant with slug '{request.Slug}' already exists" });
            }

            var result = await _provisioningService.CreateTenantAsync(
                request.Name,
                request.Slug,
                GetCurrentAdmin());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage, errors = result.Errors });
            }

            _logger.LogInformation("Created tenant {TenantName} ({Slug}) by {Admin}",
                request.Name, request.Slug, GetCurrentAdmin());

            return Ok(new
            {
                success = true,
                message = "Tenant created successfully",
                tenantId = result.TenantId,
                schemaName = result.SchemaName,
                credentials = new
                {
                    username = result.AdminUsername,
                    password = result.AdminPassword
                }
            });
        }

        /// <summary>
        /// Update tenant details.
        /// </summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantRequest request)
        {
            var tenant = await _masterDb.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound(new { success = false, message = "Tenant not found" });
            }

            tenant.Name = request.Name ?? tenant.Name;
            tenant.Notes = request.Notes;

            await _masterDb.SaveChangesAsync();

            // Invalidate cache
            _cacheService.InvalidateTenant(id);

            _logger.LogInformation("Updated tenant {TenantId} by {Admin}", id, GetCurrentAdmin());

            return Ok(new { success = true, message = "Tenant updated successfully" });
        }

        /// <summary>
        /// Activate a tenant.
        /// </summary>
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var result = await _decommissioningService.ReactivateTenantAsync(id, GetCurrentAdmin());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new { success = true, message = "Tenant activated successfully" });
        }

        /// <summary>
        /// Deactivate a tenant.
        /// </summary>
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result = await _decommissioningService.DeactivateTenantAsync(id, GetCurrentAdmin());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new { success = true, message = "Tenant deactivated successfully" });
        }

        /// <summary>
        /// Delete a tenant (permanent).
        /// </summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] bool confirm = false)
        {
            if (!confirm)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Deletion must be confirmed. Add ?confirm=true to permanently delete the tenant and all its data."
                });
            }

            var result = await _decommissioningService.DeleteTenantAsync(id, GetCurrentAdmin(), true);

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new { success = true, message = "Tenant deleted permanently" });
        }

        /// <summary>
        /// Get tenants with pending migrations.
        /// </summary>
        [HttpGet("migrations/pending")]
        public async Task<IActionResult> GetPendingMigrations()
        {
            var tenants = await _migrationService.GetTenantsWithPendingMigrationsAsync();

            return Ok(new { success = true, tenants });
        }

        /// <summary>
        /// Apply migrations to a specific tenant.
        /// </summary>
        [HttpPost("{id:int}/migrations/apply")]
        public async Task<IActionResult> ApplyMigrations(int id)
        {
            var result = await _migrationService.UpgradeTenantAsync(id, GetCurrentAdmin());

            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Ok(new
            {
                success = true,
                message = "Migrations applied successfully",
                migrationsApplied = result.MigrationsApplied,
                newVersion = result.NewVersion
            });
        }

        /// <summary>
        /// Apply migrations to all active tenants.
        /// </summary>
        [HttpPost("migrations/apply-all")]
        public async Task<IActionResult> ApplyAllMigrations()
        {
            var results = await _migrationService.UpgradeAllTenantsAsync(GetCurrentAdmin());

            var succeeded = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);

            return Ok(new
            {
                success = failed == 0,
                message = $"Migrations applied to {succeeded} tenants, {failed} failed",
                results = results.Select(r => new
                {
                    r.TenantId,
                    r.TenantName,
                    r.Success,
                    r.ErrorMessage,
                    r.MigrationsApplied,
                    r.NewVersion
                })
            });
        }

        /// <summary>
        /// Get audit logs for a tenant.
        /// </summary>
        [HttpGet("{id:int}/audit-logs")]
        public async Task<IActionResult> GetAuditLogs(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var tenant = await _masterDb.Tenants.FindAsync(id);
            if (tenant == null)
            {
                return NotFound(new { success = false, message = "Tenant not found" });
            }

            var query = _masterDb.TenantAuditLogs
                .Where(l => l.TenantId == id)
                .OrderByDescending(l => l.Timestamp);

            var total = await query.CountAsync();

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.Operation,
                    l.PerformedBy,
                    l.IsSuccess,
                    l.Details,
                    l.ErrorMessage,
                    l.Timestamp
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                logs,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }
    }

    public class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
    }

    public class UpdateTenantRequest
    {
        public string? Name { get; set; }
        public string? Notes { get; set; }
    }
}
