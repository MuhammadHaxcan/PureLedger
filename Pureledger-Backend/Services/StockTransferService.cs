using System.Data;
using PureLedger.Helpers;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.StockTransfer;

namespace PureLedger.Services;

public sealed class StockTransferValidationException : Exception
{
    public StockTransferValidationException(string message) : base(message) { }
}

public sealed class StockTransferConflictException : Exception
{
    public StockTransferConflictException(string message) : base(message) { }
}

public sealed class StockTransferService : IStockTransferService
{
    public const string ReceiptReferenceType = "StockTransferReceipt";
    public const string ChargeReferenceType = "StockTransferCharge";
    public const string CancellationReferenceType = "StockTransferCancellation";

    private static readonly string[] StockReferenceTypes =
        { ReceiptReferenceType, ChargeReferenceType, CancellationReferenceType };

    private const int ProjectionBatchSize = 200;
    private const int NormalAccountGroup = 1;
    private const int ProductAccountGroup = 2;
    private const int ExpenseAccountGroup = 3;
    private readonly LedgerDbContext _context;
    private readonly ILogger<StockTransferService> _logger;

    public StockTransferService(LedgerDbContext context, ILogger<StockTransferService> logger)
        => (_context, _logger) = (context, logger);

    public async Task<object> GetInitAsync(int userId, bool isAdmin)
    {
        var projects = isAdmin
            ? await _context.Projects.AsNoTracking().OrderBy(x => x.ProjectName).ToListAsync()
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.Project).OrderBy(x => x.ProjectName).ToListAsync();
        var locations = await _context.Locations.AsNoTracking().OrderBy(x => x.LocationName).ToListAsync();

        return new
        {
            projects = projects.Select(x => new { id = x.ProjectId, name = x.ProjectName }),
            locations = locations.Select(x => new { id = x.LocationId, name = x.LocationName }),
            chargeTargets = new[] { "Products", "OtherAccount" },
            allocationMethods = new[] { "Equal", "ByQuantity", "ByRate", "ByValue", "Manual" },
            statuses = new[] { "Pending", "PartiallyReceived", "Received", "Cancelled", "ClosedShort" }
        };
    }

    public async Task<object> GetCategoriesAsync(int projectId, int? accountGroupId, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(projectId, userId, isAdmin);
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
        var query = _context.Categories.AsNoTracking()
            .Where(x => x.ProjectId == projectId && !hidden.Contains(x.CategoryId));
        if (accountGroupId.HasValue)
        {
            query = query.Where(x => x.Accounts.Any(account =>
                account.AccountGroupId == accountGroupId.Value && account.IsEnabled));
        }

        return await query
            .OrderBy(x => x.CategoryName)
            .Select(x => new { id = x.CategoryId, name = x.CategoryName })
            .ToListAsync();
    }

    public async Task<object> GetAccountsAsync(int categoryId, int? accountGroupId, int userId, bool isAdmin)
    {
        var category = await _context.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.CategoryId == categoryId)
            ?? throw new StockTransferValidationException("Category not found.");
        await EnsureProjectAccessAsync(category.ProjectId, userId, isAdmin);
        if ((await GetHiddenCategoryIdsAsync(userId, isAdmin)).Contains(categoryId))
            throw new StockTransferValidationException("Category is not accessible.");

        var query = _context.Accounts.AsNoTracking().Where(x => x.CategoryId == categoryId && x.IsEnabled);
        if (accountGroupId.HasValue) query = query.Where(x => x.AccountGroupId == accountGroupId.Value);
        return await query.OrderBy(x => x.AccountName)
            .Select(x => new { id = x.AccountId, name = x.AccountName, categoryId = x.CategoryId, accountGroupId = x.AccountGroupId })
            .ToListAsync();
    }

    public async Task<object> GetProjectAccountsAsync(int projectId, int accountGroupId, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(projectId, userId, isAdmin);
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
        return await _context.Accounts.AsNoTracking()
            .Where(x => x.Category!.ProjectId == projectId && x.AccountGroupId == accountGroupId &&
                        x.IsEnabled && !hidden.Contains(x.CategoryId))
            .OrderBy(x => x.AccountName)
            .Select(x => new { id = x.AccountId, name = x.AccountName, categoryId = x.CategoryId, accountGroupId = x.AccountGroupId })
            .ToListAsync();
    }

    public async Task<object> GetAvailabilityAsync(int projectId, int sourceLocationId, int productAccountId,
        DateOnly? asOfDate, int? excludeDispatchJvId, int userId, bool isAdmin)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead)
            : null;
        await EnsureProjectAccessAsync(projectId, userId, isAdmin);
        var product = await GetProjectAccountAsync(projectId, productAccountId, ProductAccountGroup);
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
        if (hidden.Contains(product.CategoryId))
            throw new StockTransferValidationException("Product is not accessible.");
        await EnsureLocationAsync(sourceLocationId);

        if (excludeDispatchJvId.HasValue)
        {
            var rootId = await ResolveRootIdAsync(excludeDispatchJvId.Value);
            var graph = await LoadGraphAsync(rootId);
            await EnsureProjectAccessAsync(graph.Root.ProjectId, userId, isAdmin);
            EnsureGraphCategoryAccess(graph, hidden);
            if (graph.Root.ProjectId != projectId || DeriveStatus(graph, null) != "Pending")
                throw new StockTransferValidationException(
                    "Only a Pending dispatch in the selected project can be excluded from availability.");
            excludeDispatchJvId = rootId;
        }

        var value = await GetAvailabilityCoreAsync(projectId, sourceLocationId, productAccountId,
            asOfDate ?? BusinessClock.Today, excludeDispatchJvId);
        if (transaction != null) await transaction.CommitAsync();
        return new
        {
            productAccountId,
            productName = product.AccountName,
            availableQuantity = value.Quantity,
            inventoryValue = value.Value,
            suggestedUnitCost = value.SuggestedCost
        };
    }

    public async Task<object> GetSettingsAsync(int projectId, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(projectId, userId, isAdmin);
        var settings = await ReadTransitSettingsAsync(projectId, required: false);
        return new
        {
            projectId,
            isConfigured = settings != null,
            inTransitCategoryId = settings?.Category.CategoryId,
            inTransitCategoryName = settings?.Category.CategoryName,
            inTransitAccountId = settings?.Account.AccountId,
            inTransitAccountName = settings?.Account.AccountName
        };
    }

    public async Task<object> SaveSettingsAsync(StockTransferSettingsRequest request, int userId, bool isAdmin)
    {
        if (!isAdmin) throw new UnauthorizedAccessException("Only administrators can change stock-transfer settings.");
        await EnsureProjectAccessAsync(request.ProjectId, userId, isAdmin);
        var category = await _context.Categories.SingleOrDefaultAsync(x =>
                           x.CategoryId == request.InTransitCategoryId && x.ProjectId == request.ProjectId)
                       ?? throw new StockTransferValidationException("The in-transit category is invalid for this project.");
        var account = await _context.Accounts.SingleOrDefaultAsync(x =>
                          x.AccountId == request.InTransitAccountId && x.CategoryId == category.CategoryId &&
                          x.AccountGroupId == NormalAccountGroup && x.IsEnabled)
                      ?? throw new StockTransferValidationException("The in-transit account must be an enabled normal account in the selected category.");

        await UpsertSettingAsync(CategorySettingKey(request.ProjectId), category.CategoryId.ToString());
        await UpsertSettingAsync(AccountSettingKey(request.ProjectId), account.AccountId.ToString());
        await _context.SaveChangesAsync();
        return await GetSettingsAsync(request.ProjectId, userId, isAdmin);
    }

    public async Task<object> CreateAsync(CreateStockTransferRequest request, int userId, bool isAdmin)
    {
        await EnsureProjectAccessAsync(request.ProjectId, userId, isAdmin);
        ValidateHeader(request);
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var prepared = await PrepareDispatchAsync(request, hidden, null);
            var settings = await ReadTransitSettingsAsync(request.ProjectId, required: true)
                           ?? throw new StockTransferValidationException("Stock-transfer in-transit settings are not configured.");
            if (hidden.Contains(settings.Category.CategoryId))
                throw new StockTransferValidationException(
                    "The configured in-transit category is not accessible.");

            var root = NewVoucher(request.ProjectId, request.TransferDate);
            _context.JournalVouchers.Add(root);
            await _context.SaveChangesAsync();

            await BuildDispatchPostingAsync(root, request, prepared, settings);
            await _context.SaveChangesAsync();
            await EnsureVoucherShapeAndBalanceAsync(root.JournalVoucherId);
            await LoadGraphAsync(root.JournalVoucherId);
            await transaction.CommitAsync();
            return await GetDetailsCoreAsync(root.JournalVoucherId, null)
                   ?? throw new InvalidOperationException("Created transfer could not be reloaded.");
        }
        catch (Exception ex)
        {
            await RollbackIfActiveAsync(transaction, ex);
            ThrowTranslated(ex);
            throw;
        }
    }

    public async Task<object> EditAsync(int dispatchJvId, EditStockTransferRequest request, int userId, bool isAdmin)
    {
        ValidateHeader(request);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var root = await LockRootAsync(dispatchJvId);
            var graph = await LoadGraphAsync(root.JournalVoucherId);
            await EnsureProjectAccessAsync(root.ProjectId, userId, isAdmin);
            var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
            EnsureGraphCategoryAccess(graph, hidden);
            EnsureVersion(root, request.ExpectedRowVersion);
            if (DeriveStatus(graph, null) != "Pending")
                throw new StockTransferValidationException("Only a Pending transfer can be edited.");
            if (request.ProjectId != root.ProjectId)
                throw new StockTransferValidationException("The project cannot be changed.");
            if (request.TransferDate.Year != root.EntryDate.Year)
                throw new StockTransferValidationException("Changing the dispatch year requires delete and recreate.");

            var excludedDispatchJvId = root.JournalVoucherId;
            var prepared = await PrepareDispatchAsync(request, hidden, excludedDispatchJvId);
            // Existing transfers retain the transit account/category snapshot posted on
            // their root JV even if project settings later change.
            var settings = new TransitSettings(
                graph.TransitEntry.Account?.Category
                ?? throw new StockTransferValidationException("The dispatch transit category is missing."),
                graph.TransitEntry.Account
                ?? throw new StockTransferValidationException("The dispatch transit account is missing."));

            var dc = await _context.DeliveryChallanVouchers.Include(x => x.Entries)
                .SingleAsync(x => x.JournalVoucherId == root.JournalVoucherId);
            _context.DeliveryChallanEntries.RemoveRange(dc.Entries);
            _context.DeliveryChallanVouchers.Remove(dc);
            _context.JournalEntries.RemoveRange(await _context.JournalEntries.Where(x => x.JournalVoucherId == root.JournalVoucherId).ToListAsync());
            await _context.SaveChangesAsync();

            root.EntryDate = request.TransferDate;
            root.CreditableDate = request.TransferDate.AddDays(1);
            RotateVersion(root);
            await BuildDispatchPostingAsync(root, request, prepared, settings);
            await _context.SaveChangesAsync();
            await EnsureVoucherShapeAndBalanceAsync(root.JournalVoucherId);
            await LoadGraphAsync(root.JournalVoucherId);
            await transaction.CommitAsync();
            return await GetDetailsCoreAsync(root.JournalVoucherId, null)
                   ?? throw new InvalidOperationException("Updated transfer could not be reloaded.");
        }
        catch (Exception ex)
        {
            await RollbackIfActiveAsync(transaction, ex);
            ThrowTranslated(ex);
            throw;
        }
    }

    public async Task<object> GetListAsync(StockTransferListRequest request, int userId, bool isAdmin)
    {
        var statusGroup = request.StatusGroup?.Trim();
        if (!string.IsNullOrEmpty(statusGroup))
        {
            if (statusGroup.Equals("Open", StringComparison.OrdinalIgnoreCase))
                request.StatusGroup = "Open";
            else if (statusGroup.Equals("Final", StringComparison.OrdinalIgnoreCase))
                request.StatusGroup = "Final";
            else
                throw new StockTransferValidationException("Status group must be Open or Final.");
        }

        var projectIds = isAdmin
            ? null
            : await _context.UserProjects.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.ProjectId).ToListAsync();
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
        var roots = _context.JournalVouchers.AsNoTracking()
            .Where(x => x.IsStockTransfer &&
                        !x.IncomingReferences.Any(r => StockReferenceTypes.Contains(r.ReferenceType!)) &&
                        (isAdmin || projectIds!.Contains(x.ProjectId)));
        if (request.ProjectId.HasValue) roots = roots.Where(x => x.ProjectId == request.ProjectId.Value);
        if (request.FromDate.HasValue) roots = roots.Where(x => x.EntryDate >= request.FromDate.Value);
        if (request.ToDate.HasValue) roots = roots.Where(x => x.EntryDate <= request.ToDate.Value);
        if (request.LinkedJournalVoucherId.HasValue)
        {
            var resolved = await ResolveRootIdAsync(request.LinkedJournalVoucherId.Value);
            roots = roots.Where(x => x.JournalVoucherId == resolved);
        }

        var rootIds = await roots.OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.JournalVoucherId)
            .Select(x => x.JournalVoucherId).ToListAsync();
        var projections = (await GetProjectionsAsync(rootIds, request.AsOfDate))
            .Where(x => !x.AllCategoryIds.Any(hidden.Contains) && Matches(x, request))
            .ToList();

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 2500);
        var totalCount = projections.Count;
        var pageRows = projections.Skip(page * pageSize).Take(pageSize).Select(x => MapProjection(x)).ToList();
        return new
        {
            items = pageRows,
            totalCount,
            page,
            pageSize,
            hasMoreData = (page + 1) * pageSize < totalCount,
            totals = new
            {
                dispatchedQuantity = projections.Sum(x => x.Items.Sum(i => i.Quantity)),
                receivedQuantity = projections.Sum(x => x.Items.Where(i => i.LineState == "Received").Sum(i => i.Quantity)),
                cancelledQuantity = projections.Sum(x => x.Items.Where(i => i.LineState == "Cancelled").Sum(i => i.Quantity)),
                pendingQuantity = projections.Sum(x => x.Items.Where(i => i.LineState == "Pending").Sum(i => i.Quantity)),
                pendingValue = RoundMoney(projections.Sum(x => x.Items.Where(i => i.LineState == "Pending").Sum(i => i.BaseValue)))
            }
        };
    }

    public async Task<object?> GetDetailsAsync(int dispatchJvId, int userId, bool isAdmin)
    {
        var rootId = await ResolveRootIdAsync(dispatchJvId);
        var graph = await LoadGraphAsync(rootId);
        await EnsureProjectAccessAsync(graph.Root.ProjectId, userId, isAdmin);
        var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
        EnsureGraphCategoryAccess(graph, hidden);
        return await GetDetailsCoreAsync(rootId, null);
    }

    public async Task<object> ReceiveAsync(int dispatchJvId, ReceiveStockTransferRequest request, int userId, bool isAdmin)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var rootId = await ResolveRootIdAsync(dispatchJvId);
            var root = await LockRootAsync(rootId);
            var graph = await LoadGraphAsync(rootId);
            await EnsureProjectAccessAsync(root.ProjectId, userId, isAdmin);
            var hidden = await GetHiddenCategoryIdsAsync(userId, isAdmin);
            EnsureGraphCategoryAccess(graph, hidden);
            EnsureVersion(root, request.ExpectedRowVersion);
            var status = DeriveStatus(graph, null);
            if (status is not ("Pending" or "PartiallyReceived"))
                throw new StockTransferValidationException("This transfer is not open for receiving.");
            if (request.ReceiptDate < root.EntryDate || request.ReceiptDate > BusinessClock.Today)
                throw new StockTransferValidationException("Receipt date must be between dispatch date and today.");

            var selected = request.SelectedProductAccountIds.Distinct().ToList();
            if (selected.Count != request.SelectedProductAccountIds.Count || selected.Count == 0)
                throw new StockTransferValidationException("Select one or more unique pending products.");
            var lines = BuildLines(graph, null).ToDictionary(x => x.Entry.AccountId);
            if (selected.Any(id => !lines.TryGetValue(id, out var line) || line.State != "Pending"))
                throw new StockTransferConflictException("One or more selected products have already been received or cancelled.");
            if (selected.Any(id => hidden.Contains(lines[id].Entry.CategoryId)))
                throw new StockTransferValidationException("Receipt contains a product that is not accessible.");

            var receipt = NewVoucher(root.ProjectId, request.ReceiptDate);
            _context.JournalVouchers.Add(receipt);
            await _context.SaveChangesAsync();
            var number = TransferNumber(root);
            decimal total = 0;
            foreach (var id in selected)
            {
                var source = lines[id].Entry;
                _context.JournalEntries.Add(NewEntry(receipt.JournalVoucherId, source.Account!, source.Quantity, source.Rate,
                    source.Credit, 0, $"{number} receipt at destination"));
                total += source.Credit;
            }
            _context.JournalEntries.Add(NewEntry(receipt.JournalVoucherId, graph.TransitEntry.Account!, 0, 0,
                0, total, $"{number} released from stock in transit"));
            await _context.SaveChangesAsync();

            var receiptDc = NewChallan(receipt.JournalVoucherId, request.ReceiptDate);
            _context.DeliveryChallanVouchers.Add(receiptDc);
            await _context.SaveChangesAsync();
            var receiptProducts = await _context.JournalEntries.Where(x => x.JournalVoucherId == receipt.JournalVoucherId &&
                                                                           x.Quantity > 0 && x.Debit > 0).ToListAsync();
            foreach (var entry in receiptProducts)
                AddDce(receiptDc.DeliveryChallanVoucherId, entry.JournalEntryId, graph.DestinationLocationId,
                    request.ReceiptDate, $"{number} destination receipt");

            _context.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucherId = root.JournalVoucherId,
                ReferencedVoucherId = receipt.JournalVoucherId,
                ReferenceType = ReceiptReferenceType
            });
            await _context.SaveChangesAsync();
            await EnsureVoucherShapeAndBalanceAsync(receipt.JournalVoucherId);

            foreach (var charge in request.Charges)
                await CreateChargeVoucherAsync(root, receipt, selected, lines, charge, hidden);

            RotateVersion(root);
            await _context.SaveChangesAsync();
            await LoadGraphAsync(root.JournalVoucherId);
            await transaction.CommitAsync();
            return await GetDetailsCoreAsync(root.JournalVoucherId, null)
                   ?? throw new InvalidOperationException("Received transfer could not be reloaded.");
        }
        catch (Exception ex)
        {
            await RollbackIfActiveAsync(transaction, ex);
            ThrowTranslated(ex);
            throw;
        }
    }

    public async Task<object> CancelRemainingAsync(int dispatchJvId, StockTransferVersionRequest request, int userId, bool isAdmin)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var rootId = await ResolveRootIdAsync(dispatchJvId);
            var root = await LockRootAsync(rootId);
            var graph = await LoadGraphAsync(rootId);
            await EnsureProjectAccessAsync(root.ProjectId, userId, isAdmin);
            EnsureGraphCategoryAccess(graph, await GetHiddenCategoryIdsAsync(userId, isAdmin));
            EnsureVersion(root, request.ExpectedRowVersion);
            var pending = BuildLines(graph, null).Where(x => x.State == "Pending").ToList();
            if (pending.Count == 0) throw new StockTransferValidationException("This transfer has no pending products to cancel.");

            var cancellationDate = BusinessClock.Today;
            var cancellation = NewVoucher(root.ProjectId, cancellationDate);
            _context.JournalVouchers.Add(cancellation);
            await _context.SaveChangesAsync();
            var number = TransferNumber(root);
            decimal total = 0;
            foreach (var line in pending)
            {
                var original = line.Entry;
                _context.JournalEntries.Add(NewEntry(cancellation.JournalVoucherId, original.Account!, original.Quantity,
                    original.Rate, original.Credit, 0, $"{number} returned to source; {Clean(request.Reason)}"));
                total += original.Credit;
            }
            _context.JournalEntries.Add(NewEntry(cancellation.JournalVoucherId, graph.TransitEntry.Account!, 0, 0,
                0, total, $"{number} cancellation released from transit"));
            await _context.SaveChangesAsync();

            var dc = NewChallan(cancellation.JournalVoucherId, cancellationDate);
            _context.DeliveryChallanVouchers.Add(dc);
            await _context.SaveChangesAsync();
            var productDebits = await _context.JournalEntries.Where(x => x.JournalVoucherId == cancellation.JournalVoucherId &&
                                                                         x.Quantity > 0 && x.Debit > 0).ToListAsync();
            foreach (var entry in productDebits)
                AddDce(dc.DeliveryChallanVoucherId, entry.JournalEntryId, graph.SourceLocationId, cancellationDate,
                    $"{number} returned to source");
            _context.JournalVoucherReferences.Add(new JournalVoucherReference
            {
                MainVoucherId = root.JournalVoucherId,
                ReferencedVoucherId = cancellation.JournalVoucherId,
                ReferenceType = CancellationReferenceType
            });
            RotateVersion(root);
            await _context.SaveChangesAsync();
            await EnsureVoucherShapeAndBalanceAsync(cancellation.JournalVoucherId);
            await LoadGraphAsync(root.JournalVoucherId);
            await transaction.CommitAsync();
            return await GetDetailsCoreAsync(root.JournalVoucherId, null)
                   ?? throw new InvalidOperationException("Cancelled transfer could not be reloaded.");
        }
        catch (Exception ex)
        {
            await RollbackIfActiveAsync(transaction, ex);
            ThrowTranslated(ex);
            throw;
        }
    }

    public async Task<object> DeleteAsync(int anyLinkedJvId, DeleteStockTransferRequest request, int userId, bool isAdmin)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var rootId = await ResolveRootIdAsync(anyLinkedJvId);
            var root = await LockRootAsync(rootId);
            var graph = await LoadGraphAsync(rootId);
            await EnsureProjectAccessAsync(root.ProjectId, userId, isAdmin);
            EnsureGraphCategoryAccess(graph, await GetHiddenCategoryIdsAsync(userId, isAdmin));
            EnsureVersion(root, request.ExpectedRowVersion);

            var graphIds = graph.AllVouchers.Select(x => x.JournalVoucherId).ToHashSet();
            var external = await _context.JournalVoucherReferences.AsNoTracking()
                .AnyAsync(x => (graphIds.Contains(x.MainVoucherId) && !graphIds.Contains(x.ReferencedVoucherId)) ||
                               (graphIds.Contains(x.ReferencedVoucherId) && !graphIds.Contains(x.MainVoucherId)));
            if (external)
                throw new StockTransferValidationException("The transfer graph has an external voucher reference and cannot be safely deleted.");

            // Removing a receipt or cancellation must not remove stock already used
            // by a later sale or onward dispatch, including backdated postings.
            foreach (var productId in graph.ProductEntries.Select(e => e.AccountId).Distinct())
            {
                var costing = await new ProductInventoryCosting(_context).CalculateAsync(
                    root.ProjectId, DateOnly.MaxValue, productId: productId);
                foreach (var warehouse in costing.Movements.GroupBy(m => m.LocationId))
                {
                    int originalQuantity = 0, remainingQuantity = 0;
                    foreach (var movement in warehouse)
                    {
                        originalQuantity += movement.QuantityChange;
                        if (!graphIds.Contains(movement.Entry.JournalVoucherId)) remainingQuantity += movement.QuantityChange;
                        if (remainingQuantity < Math.Min(0, originalQuantity))
                            throw new StockTransferConflictException(
                                "This transfer supplied stock used by a later posting. Reverse the dependent sale or transfer before deleting it.");
                    }
                }
            }

            var refs = await _context.JournalVoucherReferences
                .Where(x => graphIds.Contains(x.MainVoucherId) || graphIds.Contains(x.ReferencedVoucherId)).ToListAsync();
            _context.JournalVoucherReferences.RemoveRange(refs);
            var dcs = await _context.DeliveryChallanVouchers.Include(x => x.Entries)
                .Where(x => graphIds.Contains(x.JournalVoucherId)).ToListAsync();
            _context.DeliveryChallanEntries.RemoveRange(dcs.SelectMany(x => x.Entries));
            _context.DeliveryChallanVouchers.RemoveRange(dcs);
            var entries = await _context.JournalEntries.Where(x => graphIds.Contains(x.JournalVoucherId)).ToListAsync();
            _context.JournalEntries.RemoveRange(entries);
            await _context.SaveChangesAsync();
            var children = await _context.JournalVouchers.Where(x => graphIds.Contains(x.JournalVoucherId) &&
                                                                     x.JournalVoucherId != rootId).ToListAsync();
            _context.JournalVouchers.RemoveRange(children);
            _context.JournalVouchers.Remove(root);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogWarning("Deleted complete stock-transfer graph {TransferNumber}; root {RootId}; reason {Reason}; user {UserId}",
                TransferNumber(root), rootId, Clean(request.Reason), userId);
            return new { deleted = true, dispatchJvId = rootId, id = rootId };
        }
        catch (Exception ex)
        {
            await RollbackIfActiveAsync(transaction, ex);
            ThrowTranslated(ex);
            throw;
        }
    }

    private async Task<List<PreparedLine>> PrepareDispatchAsync(CreateStockTransferRequest request, List<int> hidden,
        int? excludedDispatchJvId)
    {
        await EnsureLocationAsync(request.SourceLocationId);
        await EnsureLocationAsync(request.DestinationLocationId);
        var ids = request.Items.Select(x => x.ProductAccountId).Distinct().ToList();
        if (ids.Count != request.Items.Count)
            throw new StockTransferValidationException("A product can appear only once in a transfer.");
        var products = await _context.Accounts.Include(x => x.Category)
            .Where(x => ids.Contains(x.AccountId) && x.AccountGroupId == ProductAccountGroup && x.IsEnabled &&
                        x.Category!.ProjectId == request.ProjectId).ToDictionaryAsync(x => x.AccountId);
        if (products.Count != ids.Count)
            throw new StockTransferValidationException("One or more products are invalid for this project.");
        if (products.Values.Any(x => hidden.Contains(x.CategoryId)))
            throw new StockTransferValidationException("One or more products are not accessible.");

        var result = new List<PreparedLine>();
        foreach (var item in request.Items)
        {
            var history = await new ProductInventoryCosting(_context).CalculateAsync(request.ProjectId,
                DateOnly.MaxValue, productId: item.ProductAccountId, excludedDispatchId: excludedDispatchJvId);
            var position = history.Position(item.ProductAccountId, request.SourceLocationId,
                excludedDispatchJvId.HasValue ? request.TransferDate : request.TransferDate.AddDays(1),
                excludedDispatchJvId);
            var availability = new Availability(position.Quantity, position.Value,
                position.SuggestedRate > 0 ? RoundMoney(position.SuggestedRate) : null);
            var available = availability.Quantity;
            var suggested = availability.SuggestedCost;
            var cost = item.UnitCost.HasValue ? RoundMoney(item.UnitCost.Value) : suggested;
            if (!cost.HasValue || cost <= 0)
                throw new StockTransferValidationException($"Enter a positive transfer cost for {products[item.ProductAccountId].AccountName}.");
            var value = cost == suggested && available > 0 && availability.Value > 0
                ? ProductInventoryCosting.CarryingValue(available, availability.Value, item.Quantity)
                : RoundMoney(cost.Value * item.Quantity);
            if (value <= 0)
                throw new StockTransferValidationException("The transfer value is below the supported monetary precision.");
            result.Add(new PreparedLine(products[item.ProductAccountId], item.Quantity, cost.Value, value));
        }
        return result;
    }

    private async Task BuildDispatchPostingAsync(JournalVoucher root, CreateStockTransferRequest request,
        List<PreparedLine> lines, TransitSettings settings)
    {
        var number = TransferNumber(root);
        var total = RoundMoney(lines.Sum(x => x.BaseValue));
        var transit = NewEntry(root.JournalVoucherId, settings.Account, 0, 0, total, 0,
            $"{number} stock in transit; {Clean(request.Notes)}");
        _context.JournalEntries.Add(transit);
        foreach (var line in lines)
        {
            var amount = line.BaseValue;
            _context.JournalEntries.Add(NewEntry(root.JournalVoucherId, line.Product, line.Quantity, line.UnitCost,
                0, amount, $"{number} dispatch from source; {Clean(request.Notes)}"));
        }
        await _context.SaveChangesAsync();

        var dc = NewChallan(root.JournalVoucherId, request.TransferDate);
        _context.DeliveryChallanVouchers.Add(dc);
        await _context.SaveChangesAsync();
        var entries = await _context.JournalEntries.Where(x => x.JournalVoucherId == root.JournalVoucherId).ToListAsync();
        foreach (var product in entries.Where(x => x.Quantity > 0 && x.Credit > 0))
            AddDce(dc.DeliveryChallanVoucherId, product.JournalEntryId, request.SourceLocationId,
                request.TransferDate, $"{number} source dispatch");
        AddDce(dc.DeliveryChallanVoucherId, transit.JournalEntryId, request.DestinationLocationId,
            request.TransferDate, $"{number} intended destination route marker");
    }

    private async Task CreateChargeVoucherAsync(JournalVoucher root, JournalVoucher receipt, List<int> receivedIds,
        Dictionary<int, LineOutcome> lines, StockTransferChargeRequest request, List<int> hiddenCategories)
    {
        var target = request.TargetType?.Trim();
        if (target is not ("Products" or "OtherAccount"))
            throw new StockTransferValidationException("Charge target must be Products or OtherAccount.");
        var payee = await GetProjectChargeAccountAsync(root.ProjectId, request.PayeeAccountId);
        if (hiddenCategories.Contains(payee.CategoryId))
            throw new StockTransferValidationException("The selected charge payee is not accessible.");
        var amount = RoundMoney(request.Amount);
        if (amount <= 0) throw new StockTransferValidationException("Charge amount must be positive.");

        var voucher = NewVoucher(root.ProjectId, receipt.EntryDate);
        _context.JournalVouchers.Add(voucher);
        await _context.SaveChangesAsync();
        var note = $"{TransferNumber(root)} receiving charge; {Clean(request.Description)}";

        if (target == "OtherAccount")
        {
            if (!request.TargetAccountId.HasValue)
                throw new StockTransferValidationException("Select an account for an Other Account charge.");
            var other = await GetProjectChargeAccountAsync(root.ProjectId, request.TargetAccountId.Value);
            if (hiddenCategories.Contains(other.CategoryId))
                throw new StockTransferValidationException("The selected charge target account is not accessible.");
            if (other.AccountId == payee.AccountId)
                throw new StockTransferValidationException("Charge target and payee accounts must differ.");
            _context.JournalEntries.Add(NewEntry(voucher.JournalVoucherId, other, 0, 0, amount, 0, note));
        }
        else
        {
            var selected = request.SelectedProductAccountIds.Count > 0
                ? request.SelectedProductAccountIds.Distinct().ToList()
                : receivedIds.ToList();
            if (selected.Count == 0 || selected.Any(x => !receivedIds.Contains(x)))
                throw new StockTransferValidationException("Product charges can target only products in this receipt.");
            var allocations = CalculateAllocations(request, selected, lines, amount);
            foreach (var allocation in allocations)
            {
                var product = lines[allocation.Key].Entry.Account!;
                _context.JournalEntries.Add(NewEntry(voucher.JournalVoucherId, product, 0, 0,
                    allocation.Value, 0, note));
            }
        }

        _context.JournalEntries.Add(NewEntry(voucher.JournalVoucherId, payee, 0, 0, 0, amount, note));
        _context.JournalVoucherReferences.Add(new JournalVoucherReference
        {
            MainVoucherId = receipt.JournalVoucherId,
            ReferencedVoucherId = voucher.JournalVoucherId,
            ReferenceType = ChargeReferenceType
        });
        await _context.SaveChangesAsync();
        await EnsureVoucherShapeAndBalanceAsync(voucher.JournalVoucherId);
    }

    private static Dictionary<int, decimal> CalculateAllocations(StockTransferChargeRequest request, List<int> ids,
        Dictionary<int, LineOutcome> lines, decimal amount)
    {
        var method = request.AllocationMethod?.Trim() ?? "ByValue";
        Dictionary<int, decimal> result;
        if (method == "Manual")
        {
            if (request.Allocations.GroupBy(x => x.ProductAccountId).Any(x => x.Count() > 1) ||
                request.Allocations.Any(x => !ids.Contains(x.ProductAccountId)))
                throw new StockTransferValidationException("Manual allocations contain duplicate or unselected products.");
            result = ids.ToDictionary(id => id,
                id => RoundMoney(request.Allocations.SingleOrDefault(x => x.ProductAccountId == id)?.Amount ?? 0));
            if (result.Values.Any(value => value < 0) || result.Values.Sum() != amount)
                throw new StockTransferValidationException("Manual product allocations must equal the charge amount to four decimal places.");
            return result;
        }
        if (method is not ("Equal" or "ByQuantity" or "ByRate" or "ByValue"))
            throw new StockTransferValidationException(
                "Allocation method must be Equal, ByQuantity, ByRate, ByValue, or Manual.");

        var weights = ids.ToDictionary(id => id, id => method switch
        {
            "Equal" => 1m,
            "ByQuantity" => lines[id].Entry.Quantity,
            "ByRate" => lines[id].Entry.Rate,
            _ => lines[id].Entry.Credit
        });
        var totalWeight = weights.Values.Sum();
        if (totalWeight <= 0) throw new StockTransferValidationException("Selected products have no allocatable value or quantity.");
        return AllocateByWeight(amount, weights);
    }

    /// <summary>
    /// Splits an amount in whole 0.0001 units: every share is floored, then the leftover units go to the
    /// largest fractional remainders (ties: larger weight, then higher id). Shares are never negative and sum exactly to the amount.
    /// </summary>
    public static Dictionary<int, decimal> AllocateByWeight(decimal amount, IReadOnlyDictionary<int, decimal> weights)
    {
        const decimal unit = 0.0001m;
        var totalWeight = weights.Values.Sum();
        var totalUnits = decimal.Round(amount / unit, 0, MidpointRounding.AwayFromZero);
        var shares = weights.Select(x =>
        {
            var exact = totalUnits * x.Value / totalWeight;
            var floor = decimal.Floor(exact);
            return (x.Key, Weight: x.Value, Floor: floor, Fraction: exact - floor);
        }).ToList();
        var leftover = (int)(totalUnits - shares.Sum(x => x.Floor));
        var bonus = shares.OrderByDescending(x => x.Fraction).ThenByDescending(x => x.Weight).ThenByDescending(x => x.Key)
            .Take(leftover).Select(x => x.Key).ToHashSet();
        return shares.ToDictionary(x => x.Key, x => (x.Floor + (bonus.Contains(x.Key) ? 1 : 0)) * unit);
    }

    private async Task<Availability> GetAvailabilityCoreAsync(int projectId, int locationId, int productAccountId,
        DateOnly asOf, int? excludedDispatchJvId = null)
    {
        var costing = await new ProductInventoryCosting(_context).CalculateAsync(
            projectId, asOf, productId: productAccountId, excludedDispatchId: excludedDispatchJvId);
        var position = costing.Position(productAccountId, locationId,
            excludedDispatchJvId.HasValue ? asOf : null, excludedDispatchJvId);
        return new Availability(position.Quantity, position.Value,
            position.SuggestedRate > 0 ? RoundMoney(position.SuggestedRate) : null);
    }

    private async Task<TransferGraph> LoadGraphAsync(int rootId) =>
        (await LoadGraphsAsync(new List<int> { rootId }))[rootId];

    /// <summary>
    /// Loads and validates many transfer graphs with a fixed number of queries (a handful per call, independent
    /// of how many roots are passed). The checks are the same as for a single graph; only the loading is batched.
    /// </summary>
    private async Task<Dictionary<int, TransferGraph>> LoadGraphsAsync(IReadOnlyList<int> rootIds)
    {
        var graphs = new Dictionary<int, TransferGraph>();
        if (rootIds.Count == 0) return graphs;
        var rootIdList = rootIds.Distinct().ToList();

        var roots = (await LoadVouchersAsync(rootIdList)).ToDictionary(x => x.JournalVoucherId);
        var allRootRefs = (await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => rootIdList.Contains(x.MainVoucherId) && StockReferenceTypes.Contains(x.ReferenceType!))
            .ToListAsync()).ToLookup(x => x.MainVoucherId);

        var childIdAll = rootIdList.SelectMany(id => allRootRefs[id]).Select(x => x.ReferencedVoucherId).Distinct().ToList();
        var childById = (await LoadVouchersAsync(childIdAll)).ToDictionary(x => x.JournalVoucherId);
        var childStockRefs = (await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => childIdAll.Contains(x.MainVoucherId) && StockReferenceTypes.Contains(x.ReferenceType!))
            .ToListAsync()).ToLookup(x => x.MainVoucherId);

        var chargeIdAll = childIdAll.SelectMany(id => childStockRefs[id]).Select(x => x.ReferencedVoucherId).Distinct().ToList();
        var chargeById = (await LoadVouchersAsync(chargeIdAll)).ToDictionary(x => x.JournalVoucherId);
        var chargeOwnedRefs = (await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => chargeIdAll.Contains(x.MainVoucherId) && StockReferenceTypes.Contains(x.ReferenceType!))
            .Select(x => x.MainVoucherId).Distinct().ToListAsync()).ToHashSet();

        var everyId = rootIdList.Concat(childIdAll).Concat(chargeIdAll).Distinct().ToList();
        var parentsByReferenced = (await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => everyId.Contains(x.ReferencedVoucherId) && StockReferenceTypes.Contains(x.ReferenceType!))
            .Select(x => x.ReferencedVoucherId).ToListAsync()).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var vouchersWithInstruments = (await _context.JournalEntryInstrumentDetails.AsNoTracking()
            .Where(x => everyId.Contains(x.JournalEntry.JournalVoucherId))
            .Select(x => x.JournalEntry.JournalVoucherId).Distinct().ToListAsync()).ToHashSet();
        var dcsByVoucher = (await _context.DeliveryChallanVouchers.AsNoTracking()
            .Include(x => x.Entries).ThenInclude(x => x.Location)
            .Where(x => everyId.Contains(x.JournalVoucherId)).AsSplitQuery().ToListAsync())
            .ToLookup(x => x.JournalVoucherId);

        foreach (var rootId in rootIdList)
        {
            if (!roots.TryGetValue(rootId, out var root))
                throw new StockTransferValidationException("Stock transfer was not found.");
            if (!root.IsStockTransfer) throw new StockTransferValidationException("Voucher is not a stock transfer.");

            var rootRefs = allRootRefs[rootId].ToList();
            if (rootRefs.Any(x => x.ReferenceType == ChargeReferenceType))
                throw new StockTransferValidationException("Malformed transfer graph: dispatch cannot directly own a charge.");
            if (rootRefs.Count(x => x.ReferenceType == CancellationReferenceType) > 1)
                throw new StockTransferValidationException("Malformed transfer graph: multiple cancellation vouchers.");
            var childIds = rootRefs.Select(x => x.ReferencedVoucherId).ToList();
            var children = childIds.Distinct().Where(childById.ContainsKey).Select(id => childById[id]).ToList();
            if (children.Count != childIds.Distinct().Count() ||
                children.Any(x => !x.IsStockTransfer || x.ProjectId != root.ProjectId))
                throw new StockTransferValidationException("Malformed transfer graph: cross-project or unflagged child.");

            var receiptIds = rootRefs.Where(x => x.ReferenceType == ReceiptReferenceType).Select(x => x.ReferencedVoucherId).ToHashSet();
            var chargeRefs = childIds.Distinct().SelectMany(id => childStockRefs[id]).ToList();
            if (chargeRefs.Any(x => !receiptIds.Contains(x.MainVoucherId) ||
                                    x.ReferenceType != ChargeReferenceType))
                throw new StockTransferValidationException(
                    "Malformed transfer graph: only receipts can own stock-transfer charge references.");
            var chargeIds = chargeRefs.Select(x => x.ReferencedVoucherId).ToList();
            var charges = chargeIds.Distinct().Where(chargeById.ContainsKey).Select(id => chargeById[id]).ToList();
            if (charges.Count != chargeIds.Distinct().Count() ||
                charges.Any(x => !x.IsStockTransfer || x.ProjectId != root.ProjectId))
                throw new StockTransferValidationException("Malformed transfer graph: invalid charge voucher.");
            if (chargeIds.Any(chargeOwnedRefs.Contains))
                throw new StockTransferValidationException(
                    "Malformed transfer graph: charge vouchers cannot own stock-transfer children.");

            var graphIds = childIds.Concat(chargeIds).Append(rootId).ToHashSet();
            if (graphIds.Any(vouchersWithInstruments.Contains))
                throw new StockTransferValidationException(
                    "Payment & Receipt vouchers cannot participate in a stock-transfer graph.");
            if (graphIds.Any(id => parentsByReferenced.TryGetValue(id, out var parents) && parents > 1))
                throw new StockTransferValidationException("Malformed transfer graph: a child has multiple stock-transfer parents.");

            var dcs = graphIds.SelectMany(id => dcsByVoucher[id]).ToList();
            var products = root.JournalEntries.Where(IsDispatchProduct).ToList();
            if (products.Count == 0 || products.Select(x => x.AccountId).Distinct().Count() != products.Count)
                throw new StockTransferValidationException("Malformed dispatch: product credits must be non-empty and unique.");
            var transit = root.JournalEntries.Where(x => x.Account?.AccountGroupId == NormalAccountGroup &&
                                                         x.Quantity == 0 && x.Debit > 0 && x.Credit == 0).ToList();
            if (transit.Count != 1) throw new StockTransferValidationException("Malformed dispatch: expected one transit debit.");
            var rootDc = dcs.Where(x => x.JournalVoucherId == rootId).ToList();
            if (rootDc.Count != 1) throw new StockTransferValidationException("Malformed dispatch: expected one delivery challan.");
            var dcEntries = rootDc[0].Entries;
            var sourceIds = products.Select(product =>
            {
                var dce = dcEntries.Where(x => x.JournalEntryId == product.JournalEntryId).ToList();
                if (dce.Count != 1) throw new StockTransferValidationException("Malformed dispatch: each product requires one source location.");
                return dce[0].LocationId;
            }).Distinct().ToList();
            var route = dcEntries.Where(x => x.JournalEntryId == transit[0].JournalEntryId).ToList();
            if (sourceIds.Count != 1 || route.Count != 1 || sourceIds[0] == route[0].LocationId)
                throw new StockTransferValidationException("Malformed dispatch location routing.");

            var all = new List<JournalVoucher> { root };
            all.AddRange(children);
            all.AddRange(charges);
            var graph = new TransferGraph(root, products, transit[0], sourceIds[0], route[0].LocationId,
                rootRefs, chargeRefs, all, dcs);
            ValidateCompleteGraph(graph);
                    graphs[rootId] = graph;
        }

        return graphs;
    }

    private void ValidateCompleteGraph(TransferGraph graph)
    {
        foreach (var voucher in graph.AllVouchers)
            ValidateVoucherHeaderAndBalance(voucher);

        if (graph.Root.JournalEntries.Count != graph.ProductEntries.Count + 1 ||
            Math.Abs(graph.TransitEntry.Debit - graph.ProductEntries.Sum(x => x.Credit)) > 0.0001m)
            throw new StockTransferValidationException(
                "Malformed dispatch: product and transit entries do not form one balanced posting.");

        var rootDc = graph.DeliveryChallans.Single(x => x.JournalVoucherId == graph.Root.JournalVoucherId);
        var expectedRootEntryIds = graph.ProductEntries.Select(x => x.JournalEntryId)
            .Append(graph.TransitEntry.JournalEntryId).ToHashSet();
        if (rootDc.Entries.Count != expectedRootEntryIds.Count ||
            rootDc.Entries.Any(x => !expectedRootEntryIds.Contains(x.JournalEntryId)) ||
            graph.ProductEntries.Any(product =>
                rootDc.Entries.Count(x => x.JournalEntryId == product.JournalEntryId &&
                                          x.LocationId == graph.SourceLocationId) != 1) ||
            rootDc.Entries.Count(x => x.JournalEntryId == graph.TransitEntry.JournalEntryId &&
                                      x.LocationId == graph.DestinationLocationId) != 1)
            throw new StockTransferValidationException("Malformed dispatch delivery-challan mapping.");

        var receiptIds = graph.RootReferences.Where(x => x.ReferenceType == ReceiptReferenceType)
            .Select(x => x.ReferencedVoucherId).ToHashSet();
        var cancellationIds = graph.RootReferences.Where(x => x.ReferenceType == CancellationReferenceType)
            .Select(x => x.ReferencedVoucherId).ToHashSet();
        foreach (var voucher in graph.AllVouchers.Where(x => receiptIds.Contains(x.JournalVoucherId)))
            ValidateOutcomeVoucher(graph, voucher, graph.DestinationLocationId, "receipt");
        foreach (var voucher in graph.AllVouchers.Where(x => cancellationIds.Contains(x.JournalVoucherId)))
            ValidateOutcomeVoucher(graph, voucher, graph.SourceLocationId, "cancellation");

        var chargeIds = graph.ChargeReferences.Select(x => x.ReferencedVoucherId).ToHashSet();
        foreach (var charge in graph.AllVouchers.Where(x => chargeIds.Contains(x.JournalVoucherId)))
        {
            if (graph.DeliveryChallans.Any(x => x.JournalVoucherId == charge.JournalVoucherId))
                throw new StockTransferValidationException(
                    "Malformed charge: charge vouchers cannot own delivery challans.");
            if (charge.JournalEntries.Any(x => x.Quantity != 0))
                throw new StockTransferValidationException(
                    "Malformed charge: receiving charges cannot move product quantity.");

            var debits = charge.JournalEntries.Where(x => x.Debit > 0 && x.Credit == 0).ToList();
            var credits = charge.JournalEntries.Where(x => x.Credit > 0 && x.Debit == 0).ToList();
            if (debits.Count == 0 || credits.Count != 1 ||
                !IsChargeAccount(credits[0].Account?.AccountGroupId) ||
                !(debits.All(x => x.Account?.AccountGroupId == ProductAccountGroup) ||
                  debits.Count == 1 && IsChargeAccount(debits[0].Account?.AccountGroupId)))
                throw new StockTransferValidationException(
                    "Malformed charge: expected product allocations or one other-account debit and one payee credit.");

            var parentReceiptId = graph.ChargeReferences
                .Single(x => x.ReferencedVoucherId == charge.JournalVoucherId).MainVoucherId;
            var parentReceipt = graph.AllVouchers.Single(x => x.JournalVoucherId == parentReceiptId);
            if (charge.EntryDate != parentReceipt.EntryDate)
                throw new StockTransferValidationException(
                    "Malformed charge: charge date must match its receiving voucher.");
        }

        var outcomeIds = receiptIds.Concat(cancellationIds).ToHashSet();
        if (graph.DeliveryChallans.Any(x =>
                x.JournalVoucherId != graph.Root.JournalVoucherId &&
                !outcomeIds.Contains(x.JournalVoucherId)))
            throw new StockTransferValidationException(
                "Malformed transfer graph: an unexpected delivery challan is linked to the graph.");

        // This also proves every dispatched product has at most one full-line outcome.
        _ = BuildLines(graph, null);
    }

    private static void ValidateOutcomeVoucher(
        TransferGraph graph,
        JournalVoucher voucher,
        int expectedLocationId,
        string outcomeName)
    {
        if (voucher.EntryDate < graph.Root.EntryDate)
            throw new StockTransferValidationException(
                $"Malformed {outcomeName}: outcome date precedes the dispatch.");

        var productDebits = voucher.JournalEntries.Where(x =>
            x.Account?.AccountGroupId == ProductAccountGroup &&
            x.Quantity > 0 && x.Debit > 0 && x.Credit == 0).ToList();
        var transitCredits = voucher.JournalEntries.Where(x =>
            x.AccountId == graph.TransitEntry.AccountId &&
            x.Quantity == 0 && x.Credit > 0 && x.Debit == 0).ToList();
        if (productDebits.Count == 0 || transitCredits.Count != 1 ||
            voucher.JournalEntries.Count != productDebits.Count + 1 ||
            productDebits.Select(x => x.AccountId).Distinct().Count() != productDebits.Count ||
            Math.Abs(productDebits.Sum(x => x.Debit) - transitCredits[0].Credit) > 0.0001m)
            throw new StockTransferValidationException(
                $"Malformed {outcomeName}: expected unique product debits and one matching transit credit.");

        foreach (var outcome in productDebits)
        {
            var dispatch = graph.ProductEntries.SingleOrDefault(x => x.AccountId == outcome.AccountId);
            if (dispatch == null || outcome.Quantity != dispatch.Quantity ||
                Math.Abs(outcome.Debit - dispatch.Credit) > 0.0001m)
                throw new StockTransferValidationException(
                    $"Malformed {outcomeName}: product outcome must match one complete dispatched line.");
        }

        var dcs = graph.DeliveryChallans.Where(x => x.JournalVoucherId == voucher.JournalVoucherId).ToList();
        if (dcs.Count != 1)
            throw new StockTransferValidationException(
                $"Malformed {outcomeName}: expected one delivery challan.");
        var expectedEntryIds = productDebits.Select(x => x.JournalEntryId).ToHashSet();
        if (dcs[0].Entries.Count != expectedEntryIds.Count ||
            dcs[0].Entries.Any(x =>
                !expectedEntryIds.Contains(x.JournalEntryId) || x.LocationId != expectedLocationId) ||
            expectedEntryIds.Any(id => dcs[0].Entries.Count(x => x.JournalEntryId == id) != 1))
            throw new StockTransferValidationException(
                $"Malformed {outcomeName}: delivery-challan entries do not match the product outcome.");
    }

    private static void ValidateVoucherHeaderAndBalance(JournalVoucher voucher)
    {
        if (!voucher.IsStockTransfer || !voucher.IsCompleted || voucher.IsBaseVoucher ||
            voucher.PendingForCharges || voucher.RowVersion == null || voucher.RowVersion.Length == 0 ||
            voucher.JournalEntries.Count == 0 || voucher.JournalEntries.Any(x => !x.IsProcessed))
            throw new StockTransferValidationException(
                $"Malformed stock-transfer voucher {voucher.JournalVoucherId}: invalid header or unprocessed entries.");
        if (voucher.JournalEntries.Any(x =>
                x.Debit < 0 || x.Credit < 0 || x.Quantity < 0 ||
                x.Debit > 0 && x.Credit > 0))
            throw new StockTransferValidationException(
                $"Malformed stock-transfer voucher {voucher.JournalVoucherId}: invalid journal-entry shape.");
        if (Math.Abs(RoundMoney(voucher.JournalEntries.Sum(x => x.Debit)) -
                     RoundMoney(voucher.JournalEntries.Sum(x => x.Credit))) > 0.0001m)
            throw new StockTransferValidationException(
                $"Malformed stock-transfer voucher {voucher.JournalVoucherId}: voucher is not balanced.");
    }

    private List<LineOutcome> BuildLines(TransferGraph graph, DateOnly? asOf)
    {
        var receipts = graph.RootReferences.Where(x => x.ReferenceType == ReceiptReferenceType)
            .Select(x => graph.AllVouchers.Single(v => v.JournalVoucherId == x.ReferencedVoucherId))
            .Where(x => !asOf.HasValue || x.EntryDate <= asOf.Value).ToList();
        var cancellations = graph.RootReferences.Where(x => x.ReferenceType == CancellationReferenceType)
            .Select(x => graph.AllVouchers.Single(v => v.JournalVoucherId == x.ReferencedVoucherId))
            .Where(x => !asOf.HasValue || x.EntryDate <= asOf.Value).ToList();
        var lines = new List<LineOutcome>();
        foreach (var product in graph.ProductEntries)
        {
            var receiptMatches = receipts
                .SelectMany(voucher => voucher.JournalEntries
                    .Where(entry => entry.AccountId == product.AccountId &&
                                    entry.Quantity > 0 && entry.Debit > 0)
                    .Select(entry => (Voucher: voucher, Entry: entry)))
                .ToList();
            var cancellationMatches = cancellations
                .SelectMany(voucher => voucher.JournalEntries
                    .Where(entry => entry.AccountId == product.AccountId &&
                                    entry.Quantity > 0 && entry.Debit > 0)
                    .Select(entry => (Voucher: voucher, Entry: entry)))
                .ToList();
            if (receiptMatches.Count + cancellationMatches.Count > 1)
                throw new StockTransferValidationException("Malformed transfer graph: duplicate product outcome.");
            var outcome = receiptMatches.Select(x => x.Entry).SingleOrDefault()
                          ?? cancellationMatches.Select(x => x.Entry).SingleOrDefault();
            if (outcome != null && (outcome.Quantity != product.Quantity || Math.Abs(outcome.Debit - product.Credit) > 0.0001m))
                throw new StockTransferValidationException("Malformed transfer graph: product outcome is not the full dispatched line.");
            lines.Add(new LineOutcome(
                product,
                receiptMatches.Count == 1 ? "Received" : cancellationMatches.Count == 1 ? "Cancelled" : "Pending",
                receiptMatches.Count == 1 ? receiptMatches[0].Voucher.JournalVoucherId : null,
                cancellationMatches.Count == 1 ? cancellationMatches[0].Voucher.JournalVoucherId : null));
        }
        return lines;
    }

    private string DeriveStatus(TransferGraph graph, DateOnly? asOf)
    {
        var lines = BuildLines(graph, asOf);
        var received = lines.Count(x => x.State == "Received");
        var cancelled = lines.Count(x => x.State == "Cancelled");
        var pending = lines.Count(x => x.State == "Pending");
        if (pending == lines.Count) return "Pending";
        if (pending > 0) return "PartiallyReceived";
        if (received == lines.Count) return "Received";
        if (cancelled == lines.Count) return "Cancelled";
        return "ClosedShort";
    }

    private async Task<object?> GetDetailsCoreAsync(int rootId, DateOnly? asOf)
    {
        var projection = await GetProjectionAsync(rootId, asOf);
        return projection == null ? null : MapProjection(projection, includeLinks: true);
    }

    private async Task<TransferProjection?> GetProjectionAsync(int rootId, DateOnly? asOf)
    {
        var graph = await LoadGraphAsync(rootId);
        if (asOf.HasValue && graph.Root.EntryDate > asOf.Value) return null;
        var project = await _context.Projects.AsNoTracking().SingleAsync(x => x.ProjectId == graph.Root.ProjectId);
        var locations = await _context.Locations.AsNoTracking()
            .Where(x => x.LocationId == graph.SourceLocationId || x.LocationId == graph.DestinationLocationId)
            .ToDictionaryAsync(x => x.LocationId);
        return BuildProjection(graph, asOf, project, locations);
    }

    // Projections for a list are built from batch-loaded graphs, so a page of results costs a fixed number of
    // queries per chunk of roots instead of a dozen per transfer.
    private async Task<List<TransferProjection>> GetProjectionsAsync(IReadOnlyList<int> rootIds, DateOnly? asOf)
    {
        var projections = new List<TransferProjection>();
        foreach (var chunk in rootIds.Chunk(ProjectionBatchSize))
        {
            var graphs = await LoadGraphsAsync(chunk);
            var projectIds = graphs.Values.Select(x => x.Root.ProjectId).Distinct().ToList();
            var locationIds = graphs.Values.SelectMany(x => new[] { x.SourceLocationId, x.DestinationLocationId }).Distinct().ToList();
            var projects = await _context.Projects.AsNoTracking()
                .Where(x => projectIds.Contains(x.ProjectId)).ToDictionaryAsync(x => x.ProjectId);
            var locations = await _context.Locations.AsNoTracking()
                .Where(x => locationIds.Contains(x.LocationId)).ToDictionaryAsync(x => x.LocationId);
            foreach (var rootId in chunk)
            {
                var graph = graphs[rootId];
                if (asOf.HasValue && graph.Root.EntryDate > asOf.Value) continue;
                projections.Add(BuildProjection(graph, asOf, projects[graph.Root.ProjectId], locations));
            }
        }
        return projections;
    }

    private TransferProjection BuildProjection(TransferGraph graph, DateOnly? asOf, Project project,
        IReadOnlyDictionary<int, Location> locations)
    {
        var lines = BuildLines(graph, asOf);
        var receipts = graph.RootReferences.Where(x => x.ReferenceType == ReceiptReferenceType)
            .Select(x => graph.AllVouchers.Single(v => v.JournalVoucherId == x.ReferencedVoucherId))
            .Where(x => !asOf.HasValue || x.EntryDate <= asOf.Value).OrderBy(x => x.EntryDate).ToList();
        var cancellation = graph.RootReferences.Where(x => x.ReferenceType == CancellationReferenceType)
            .Select(x => graph.AllVouchers.Single(v => v.JournalVoucherId == x.ReferencedVoucherId))
            .SingleOrDefault(x => !asOf.HasValue || x.EntryDate <= asOf.Value);
        var receiptIds = receipts.Select(x => x.JournalVoucherId).ToHashSet();
        var chargeRefs = graph.ChargeReferences.Where(x => receiptIds.Contains(x.MainVoucherId)).ToList();
        var chargeVouchers = chargeRefs.Select(x => graph.AllVouchers.Single(v => v.JournalVoucherId == x.ReferencedVoucherId)).ToList();
        return new TransferProjection
        {
            Root = graph.Root,
            TransferNumber = TransferNumber(graph.Root),
            ProjectName = project.ProjectName,
            SourceLocationId = graph.SourceLocationId,
            SourceLocationName = locations[graph.SourceLocationId].LocationName,
            DestinationLocationId = graph.DestinationLocationId,
            DestinationLocationName = locations[graph.DestinationLocationId].LocationName,
            Status = DeriveStatus(graph, asOf),
            Items = lines.Select(x => new ProjectionLine
            {
                ProductAccountId = x.Entry.AccountId,
                ProductName = x.Entry.Account!.AccountName,
                CategoryId = x.Entry.CategoryId,
                CategoryName = x.Entry.Account.Category?.CategoryName,
                Quantity = x.Entry.Quantity,
                UnitCost = x.Entry.Rate,
                BaseValue = x.Entry.Credit,
                LineState = x.State,
                ReceiptJournalVoucherId = x.ReceiptJournalVoucherId,
                CancellationJournalVoucherId = x.CancellationJournalVoucherId
            }).ToList(),
            ReceiptVouchers = receipts,
            CancellationVoucher = cancellation,
            ChargeVouchers = chargeVouchers,
            ChargeReferences = chargeRefs,
            DeliveryChallanIds = graph.DeliveryChallans
                .ToDictionary(x => x.JournalVoucherId, x => x.DeliveryChallanVoucherId),
            AllCategoryIds = graph.AllVouchers
                .SelectMany(x => x.JournalEntries)
                .Select(x => x.CategoryId)
                .Distinct()
                .ToHashSet(),
            AsOfDate = asOf
        };
    }

    private static object MapProjection(TransferProjection x, bool includeLinks = false)
    {
        var rowVersion = EncodeVersion(x.Root.RowVersion);
        var pending = x.Items.Where(i => i.LineState == "Pending").ToList();
        var chargeRows = x.ChargeVouchers.Select(v =>
        {
            var debits = v.JournalEntries.Where(e => e.Debit > 0).ToList();
            var payee = v.JournalEntries.Single(e => e.Credit > 0);
            return new
            {
                chargeJournalVoucherId = v.JournalVoucherId,
                receiptJournalVoucherId = x.ChargeReferences.Single(r => r.ReferencedVoucherId == v.JournalVoucherId).MainVoucherId,
                date = v.EntryDate,
                amount = payee.Credit,
                payeeAccountId = payee.AccountId,
                payeeAccountName = payee.Account?.AccountName,
                targetType = debits.All(e => e.Account?.AccountGroupId == ProductAccountGroup) ? "Products" : "OtherAccount",
                allocations = debits.Select(e => new { accountId = e.AccountId, accountName = e.Account?.AccountName, amount = e.Debit }),
                description = payee.Description
            };
        }).ToList();
        return new
        {
            id = x.Root.JournalVoucherId,
            dispatchJvId = x.Root.JournalVoucherId,
            x.TransferNumber,
            projectId = x.Root.ProjectId,
            x.ProjectName,
            x.SourceLocationId,
            x.SourceLocationName,
            x.DestinationLocationId,
            x.DestinationLocationName,
            transferDate = x.Root.EntryDate,
            notes = ExtractNote(x.Root.JournalEntries.Select(e => e.Description)),
            x.Status,
            rowVersion,
            version = rowVersion,
            isPending = x.Status == "Pending",
            canEdit = x.Status == "Pending",
            canReceive = x.Status is "Pending" or "PartiallyReceived",
            canCancel = pending.Count > 0,
            canDelete = true,
            dispatchedQuantity = x.Items.Sum(i => i.Quantity),
            receivedQuantity = x.Items.Where(i => i.LineState == "Received").Sum(i => i.Quantity),
            cancelledQuantity = x.Items.Where(i => i.LineState == "Cancelled").Sum(i => i.Quantity),
            pendingQuantity = pending.Sum(i => i.Quantity),
            dispatchedValue = RoundMoney(x.Items.Sum(i => i.BaseValue)),
            pendingValue = RoundMoney(pending.Sum(i => i.BaseValue)),
            dispatchDeliveryChallanId = GetDeliveryChallanId(x, x.Root.JournalVoucherId),
            items = x.Items,
            receipts = includeLinks
                ? x.ReceiptVouchers.Select(v => new
                {
                    receiptJournalVoucherId = v.JournalVoucherId,
                    deliveryChallanId = GetDeliveryChallanId(x, v.JournalVoucherId),
                    receiptDate = v.EntryDate,
                    productAccountIds = v.JournalEntries.Where(e => e.Account?.AccountGroupId == ProductAccountGroup &&
                                                                     e.Quantity > 0 && e.Debit > 0).Select(e => e.AccountId),
                    charges = chargeRows.Where(c => c.receiptJournalVoucherId == v.JournalVoucherId)
                })
                : null,
            cancellationJournalVoucherId = x.CancellationVoucher?.JournalVoucherId,
            cancellationDeliveryChallanId = x.CancellationVoucher == null
                ? null
                : GetDeliveryChallanId(x, x.CancellationVoucher.JournalVoucherId),
            charges = includeLinks ? chargeRows : null,
            asOfDate = x.AsOfDate
        };
    }

    private static int? GetDeliveryChallanId(TransferProjection projection, int voucherId) =>
        projection.DeliveryChallanIds.TryGetValue(voucherId, out var deliveryChallanId)
            ? deliveryChallanId
            : null;

    private static bool Matches(TransferProjection x, StockTransferListRequest request)
    {
        if (request.StatusGroup?.Trim() == "Open" &&
            x.Status is not ("Pending" or "PartiallyReceived")) return false;
        if (request.StatusGroup?.Trim() == "Final" &&
            x.Status is not ("Received" or "Cancelled" or "ClosedShort")) return false;
        if (!string.IsNullOrWhiteSpace(request.Status) && !x.Status.Equals(request.Status, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.ItemState) && !x.Items.Any(i => i.LineState.Equals(request.ItemState, StringComparison.OrdinalIgnoreCase))) return false;
        if (request.CategoryId.HasValue && !x.Items.Any(i => i.CategoryId == request.CategoryId.Value)) return false;
        if (request.ProductAccountId.HasValue && !x.Items.Any(i => i.ProductAccountId == request.ProductAccountId.Value)) return false;
        if (request.LocationId.HasValue &&
            x.SourceLocationId != request.LocationId.Value &&
            x.DestinationLocationId != request.LocationId.Value) return false;
        if (request.SourceLocationId.HasValue && x.SourceLocationId != request.SourceLocationId.Value) return false;
        if (request.DestinationLocationId.HasValue && x.DestinationLocationId != request.DestinationLocationId.Value) return false;
        if (request.HasCharges.HasValue && (x.ChargeVouchers.Count > 0) != request.HasCharges.Value) return false;
        if (!string.IsNullOrWhiteSpace(request.TransferNumber) &&
            !x.TransferNumber.Contains(request.TransferNumber.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var contains = (string? value) =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Contains(term, StringComparison.OrdinalIgnoreCase);
            var linkedVoucherIds = x.ReceiptVouchers.Select(v => v.JournalVoucherId)
                .Concat(x.ChargeVouchers.Select(v => v.JournalVoucherId))
                .Concat(x.CancellationVoucher == null
                    ? Enumerable.Empty<int>()
                    : new[] { x.CancellationVoucher.JournalVoucherId });
            var matchesSearch =
                contains(x.TransferNumber) ||
                contains(x.ProjectName) ||
                contains(x.SourceLocationName) ||
                contains(x.DestinationLocationName) ||
                contains(x.Status) ||
                contains($"JV {x.Root.JournalVoucherId}") ||
                contains(x.Root.JournalVoucherId.ToString()) ||
                linkedVoucherIds.Any(id => contains($"JV {id}") || contains(id.ToString())) ||
                x.Items.Any(item =>
                    contains(item.ProductName) ||
                    contains(item.CategoryName) ||
                    contains(item.LineState)) ||
                x.Root.JournalEntries.Any(entry => contains(entry.Description)) ||
                x.ReceiptVouchers.Concat(x.ChargeVouchers)
                    .Append(x.CancellationVoucher)
                    .Where(voucher => voucher != null)
                    .SelectMany(voucher => voucher!.JournalEntries)
                    .Any(entry => contains(entry.Description));
            if (!matchesSearch) return false;
        }
        if (request.ReceiptFromDate.HasValue && !x.ReceiptVouchers.Any(v => v.EntryDate >= request.ReceiptFromDate.Value)) return false;
        if (request.ReceiptToDate.HasValue && !x.ReceiptVouchers.Any(v => v.EntryDate <= request.ReceiptToDate.Value)) return false;
        return true;
    }

    private async Task<int> ResolveRootIdAsync(int voucherId)
    {
        if (await _context.JournalEntryInstrumentDetails.AsNoTracking()
                .AnyAsync(x => x.JournalEntry.JournalVoucherId == voucherId))
            throw new StockTransferValidationException(
                "Payment & Receipt vouchers cannot be processed as stock transfers.");
        var voucher = await _context.JournalVouchers.AsNoTracking()
                          .SingleOrDefaultAsync(x => x.JournalVoucherId == voucherId)
                      ?? throw new StockTransferValidationException("Voucher was not found.");
        if (!voucher.IsStockTransfer) throw new StockTransferValidationException("Voucher is not a stock transfer.");
        var incoming = await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.ReferencedVoucherId == voucherId && StockReferenceTypes.Contains(x.ReferenceType!)).ToListAsync();
        if (incoming.Count == 0) return voucherId;
        if (incoming.Count != 1) throw new StockTransferValidationException("Malformed stock-transfer graph: multiple parents.");
        if (incoming[0].ReferenceType is ReceiptReferenceType or CancellationReferenceType)
            return incoming[0].MainVoucherId;
        var receiptId = incoming[0].MainVoucherId;
        var parent = await _context.JournalVoucherReferences.AsNoTracking()
            .Where(x => x.ReferencedVoucherId == receiptId && x.ReferenceType == ReceiptReferenceType).ToListAsync();
        if (parent.Count != 1) throw new StockTransferValidationException("Malformed stock-transfer charge graph.");
        return parent[0].MainVoucherId;
    }

    private async Task<JournalVoucher> LockRootAsync(int rootId)
    {
        var root = await RowLocks.LockJournalVoucherAsync(_context, rootId);
        if (root == null || !root.IsStockTransfer)
            throw new StockTransferValidationException("Stock transfer was not found.");
        return root;
    }

    private Task<JournalVoucher?> LoadVoucherAsync(int id) => _context.JournalVouchers.AsNoTracking()
        .Include(x => x.JournalEntries).ThenInclude(x => x.Account).ThenInclude(x => x!.Category)
        .AsSplitQuery().SingleOrDefaultAsync(x => x.JournalVoucherId == id);

    private async Task<List<JournalVoucher>> LoadVouchersAsync(List<int> ids)
    {
        if (ids.Count == 0) return new List<JournalVoucher>();
        return await _context.JournalVouchers.AsNoTracking()
            .Include(x => x.JournalEntries).ThenInclude(x => x.Account).ThenInclude(x => x!.Category)
            .Where(x => ids.Contains(x.JournalVoucherId)).AsSplitQuery().ToListAsync();
    }

    private async Task<TransitSettings?> ReadTransitSettingsAsync(int projectId, bool required)
    {
        var keys = new[] { CategorySettingKey(projectId), AccountSettingKey(projectId) };
        var values = await _context.AppSettings.AsNoTracking().Where(x => keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        if (!values.TryGetValue(keys[0], out var categoryValue) || !int.TryParse(categoryValue, out var categoryId) ||
            !values.TryGetValue(keys[1], out var accountValue) || !int.TryParse(accountValue, out var accountId))
        {
            if (required) throw new StockTransferValidationException("Configure the project's Stock In Transit category and account first.");
            return null;
        }
        var category = await _context.Categories.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CategoryId == categoryId && x.ProjectId == projectId);
        var account = await _context.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.CategoryId == categoryId &&
                                       x.AccountGroupId == NormalAccountGroup && x.IsEnabled);
        if (category == null || account == null)
        {
            if (required) throw new StockTransferValidationException("The project's Stock In Transit setting is invalid.");
            return null;
        }
        return new TransitSettings(category, account);
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await _context.AppSettings.SingleOrDefaultAsync(x => x.SettingKey == key);
        if (setting == null)
            _context.AppSettings.Add(new AppSetting { SettingKey = key, SettingValue = value, UpdatedAt = DateTime.UtcNow });
        else
        {
            setting.SettingValue = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<Account> GetProjectAccountAsync(int projectId, int accountId, int groupId) =>
        await _context.Accounts.Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.AccountGroupId == groupId &&
                                       x.IsEnabled && x.Category!.ProjectId == projectId)
        ?? throw new StockTransferValidationException("Selected account is invalid or does not belong to this project.");

    private async Task<Account> GetProjectChargeAccountAsync(int projectId, int accountId) =>
        await _context.Accounts.Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.AccountId == accountId &&
                                       (x.AccountGroupId == NormalAccountGroup ||
                                        x.AccountGroupId == ExpenseAccountGroup) &&
                                       x.IsEnabled && x.Category!.ProjectId == projectId)
        ?? throw new StockTransferValidationException(
            "Selected charge account must be an enabled Account or Expense account in this project.");

    private static bool IsChargeAccount(int? groupId) =>
        groupId == NormalAccountGroup || groupId == ExpenseAccountGroup;

    private async Task EnsureProjectAccessAsync(int projectId, int userId, bool isAdmin)
    {
        if (!await _context.Projects.AsNoTracking().AnyAsync(x => x.ProjectId == projectId))
            throw new StockTransferValidationException("Project was not found.");
        if (!isAdmin && !await _context.UserProjects.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ProjectId == projectId))
            throw new UnauthorizedAccessException("You do not have access to this project.");
    }

    private async Task EnsureLocationAsync(int locationId)
    {
        if (!await _context.Locations.AsNoTracking().AnyAsync(x => x.LocationId == locationId))
            throw new StockTransferValidationException("Location was not found.");
    }

    private async Task<List<int>> GetHiddenCategoryIdsAsync(int userId, bool isAdmin) => isAdmin
        ? new List<int>()
        : await _context.UserCategories.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.CategoryId).ToListAsync();

    private static void EnsureGraphCategoryAccess(
        TransferGraph graph,
        IReadOnlyCollection<int> hiddenCategoryIds)
    {
        if (hiddenCategoryIds.Count == 0) return;
        if (graph.AllVouchers.SelectMany(x => x.JournalEntries)
            .Any(x => hiddenCategoryIds.Contains(x.CategoryId)))
            throw new StockTransferValidationException(
                "Transfer contains entries from categories that are not accessible.");
    }

    private static JournalVoucher NewVoucher(int projectId, DateOnly date) => new()
    {
        ProjectId = projectId,
        EntryDate = date,
        CreditableDate = date.AddDays(1),
        IsCompleted = true,
        IsBaseVoucher = false,
        PendingForCharges = false,
        IsStockTransfer = true,
        RowVersion = NewVersion()
    };

    private static DeliveryChallanVoucher NewChallan(int voucherId, DateOnly date) => new()
    {
        JournalVoucherId = voucherId,
        DCDate = date,
        FinalDeliveryDate = date,
        IsCompleted = true
    };

    private static JournalEntry NewEntry(int voucherId, Account account, int quantity, decimal rate,
        decimal debit, decimal credit, string description) => new()
    {
        JournalVoucherId = voucherId,
        AccountId = account.AccountId,
        CategoryId = account.CategoryId,
        Quantity = quantity,
        Rate = RoundMoney(rate),
        Debit = RoundMoney(debit),
        Credit = RoundMoney(credit),
        Discount = 0,
        DiscountPercent = 0,
        Rbp = "N",
        IsProcessed = true,
        Description = description.Length <= 500 ? description : description[..500]
    };

    private void AddDce(int dcId, int entryId, int locationId, DateOnly date, string remarks) =>
        _context.DeliveryChallanEntries.Add(new DeliveryChallanEntry
        {
            DeliveryChallanVoucherId = dcId,
            JournalEntryId = entryId,
            LocationId = locationId,
            EntryDate = date,
            Remarks = remarks.Length <= 500 ? remarks : remarks[..500]
        });

    private async Task EnsureVoucherShapeAndBalanceAsync(int voucherId)
    {
        var voucher = await _context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.JournalVoucherId == voucherId);
        if (!voucher.IsStockTransfer || !voucher.IsCompleted || voucher.IsBaseVoucher ||
            voucher.JournalEntries.Count == 0 || voucher.JournalEntries.Any(x => !x.IsProcessed))
            throw new InvalidOperationException("Generated stock-transfer voucher is not completed and processed.");
        var debit = RoundMoney(voucher.JournalEntries.Sum(x => x.Debit));
        var credit = RoundMoney(voucher.JournalEntries.Sum(x => x.Credit));
        if (Math.Abs(debit - credit) > 0.0001m)
            throw new InvalidOperationException($"Generated journal voucher {voucherId} is not balanced.");
    }

    private static bool IsDispatchProduct(JournalEntry x) =>
        x.Account?.AccountGroupId == ProductAccountGroup && x.Quantity > 0 && x.Credit > 0 && x.Debit == 0;

    private static void ValidateHeader(CreateStockTransferRequest request)
    {
        if (request.ProjectId <= 0) throw new StockTransferValidationException("Project is required.");
        if (request.SourceLocationId == request.DestinationLocationId)
            throw new StockTransferValidationException("Source and destination locations must differ.");
        if (request.TransferDate > BusinessClock.Today)
            throw new StockTransferValidationException("Transfer date cannot be in the future.");
        if (request.Items.Count == 0) throw new StockTransferValidationException("Add at least one product.");
        if (request.Items.Any(x => x.Quantity <= 0))
            throw new StockTransferValidationException("Product quantities must be positive whole numbers.");
    }

    private static void EnsureVersion(JournalVoucher root, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(NormalizeBase64(expected)),
                root.RowVersion ?? Array.Empty<byte>()))
            throw new StockTransferConflictException("The transfer changed since it was opened. Refresh and try again.");
    }

    private static string NormalizeBase64(string value)
    {
        try { Convert.FromBase64String(value.Trim()); return value.Trim(); }
        catch (FormatException) { throw new StockTransferConflictException("The transfer version is invalid. Refresh and try again."); }
    }

    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static void RotateVersion(JournalVoucher root) => root.RowVersion = NewVersion();
    private static string EncodeVersion(byte[]? value) => Convert.ToBase64String(value ?? Array.Empty<byte>());
    private static string TransferNumber(JournalVoucher root) => $"ST-{root.EntryDate.Year}-{root.JournalVoucherId:000000}";
    private static string CategorySettingKey(int projectId) => $"StockTransfer:{projectId}:InTransitCategoryId";
    private static string AccountSettingKey(int projectId) => $"StockTransfer:{projectId}:InTransitAccountId";
    private static decimal RoundMoney(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ExtractNote(IEnumerable<string?> descriptions)
    {
        foreach (var description in descriptions)
        {
            if (string.IsNullOrWhiteSpace(description)) continue;
            var separator = description.IndexOf(';');
            if (separator < 0 || separator == description.Length - 1) continue;
            var note = description[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(note)) return note;
        }
        return null;
    }

    private static async Task RollbackIfActiveAsync(IDbContextTransaction transaction, Exception original)
    {
        // PostgreSQL may already have ended the transaction after a failed commit.
        // Preserve the serialization error so callers receive a refresh conflict.
        try { await transaction.RollbackAsync(); }
        catch (InvalidOperationException) when (IsDatabaseConflict(original)) { }
    }

    private static void ThrowTranslated(Exception ex)
    {
        if (ex is StockTransferValidationException or StockTransferConflictException or UnauthorizedAccessException) return;
        if (ex is DbUpdateConcurrencyException || IsDatabaseConflict(ex))
            throw new StockTransferConflictException("Stock changed during this operation. Refresh and try again.");
    }

    private static bool IsDatabaseConflict(Exception ex)
    {
        if (ex is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.SerializationFailure || pg.SqlState == PostgresErrorCodes.DeadlockDetected))
            return true;
        return ex.InnerException != null && IsDatabaseConflict(ex.InnerException);
    }

    private sealed record PreparedLine(Account Product, int Quantity, decimal UnitCost, decimal BaseValue);
    private sealed record TransitSettings(Category Category, Account Account);
    private sealed record Availability(int Quantity, decimal Value, decimal? SuggestedCost);
    private sealed record LineOutcome(
        JournalEntry Entry,
        string State,
        int? ReceiptJournalVoucherId = null,
        int? CancellationJournalVoucherId = null);
    private sealed record TransferGraph(
        JournalVoucher Root,
        List<JournalEntry> ProductEntries,
        JournalEntry TransitEntry,
        int SourceLocationId,
        int DestinationLocationId,
        List<JournalVoucherReference> RootReferences,
        List<JournalVoucherReference> ChargeReferences,
        List<JournalVoucher> AllVouchers,
        List<DeliveryChallanVoucher> DeliveryChallans);

    private sealed class ProjectionLine
    {
        public int ProductAccountId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public int CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public int Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public decimal BaseValue { get; init; }
        public string LineState { get; init; } = string.Empty;
        public int? ReceiptJournalVoucherId { get; init; }
        public int? CancellationJournalVoucherId { get; init; }
    }

    private sealed class TransferProjection
    {
        public JournalVoucher Root { get; init; } = null!;
        public string TransferNumber { get; init; } = string.Empty;
        public string ProjectName { get; init; } = string.Empty;
        public int SourceLocationId { get; init; }
        public string SourceLocationName { get; init; } = string.Empty;
        public int DestinationLocationId { get; init; }
        public string DestinationLocationName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public List<ProjectionLine> Items { get; init; } = new();
        public List<JournalVoucher> ReceiptVouchers { get; init; } = new();
        public JournalVoucher? CancellationVoucher { get; init; }
        public List<JournalVoucher> ChargeVouchers { get; init; } = new();
        public List<JournalVoucherReference> ChargeReferences { get; init; } = new();
        public Dictionary<int, int> DeliveryChallanIds { get; init; } = new();
        public HashSet<int> AllCategoryIds { get; init; } = new();
        public DateOnly? AsOfDate { get; init; }
    }
}
