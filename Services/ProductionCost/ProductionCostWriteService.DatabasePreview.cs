using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Valuation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost;

public sealed partial class ProductionCostWriteService
{
    public Task<ProductionCostEnsureResult> EnsureDraftRunAsync(
        string companyCd, string? userId, string periodYm, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PeriodYm must be yyyyMM.");
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.EnsureDraftRunAsync(session, companyCd, periodYm.Trim(), userId));
    }

    public Task UpdateRunConfigAsync(
        string companyCd, string? userId, ProductionCostRunConfigUpdate update, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        ArgumentNullException.ThrowIfNull(update);
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.UpdateRunConfigAsync(session, companyCd, update, userId));
    }

    public Task UpdateObjectSelectionAsync(
        string companyCd, string? userId, ProductionCostObjectSelectionUpdate update, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.UpdateObjectSelectionAsync(session, companyCd, update, userId));
    }

    public Task<int> SyncObjectsFromReceiptsAsync(
        string companyCd, string? userId, long runId, int runVersion, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.SyncObjectsFromReceiptsAsync(session, companyCd, runId, runVersion, userId));
    }

    public Task UpdatePoolReductionAsync(
        string companyCd, string? userId, ProductionCostPoolReductionUpdate update, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.UpdatePoolReductionAsync(session, companyCd, update, userId));
    }

    public Task SaveWipAdjustmentAsync(
        string companyCd, string? userId, ProductionCostWipAdjustmentUpdate update, string? databaseName)
    {
        EnsureAuth(companyCd);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        return ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            (session, _) => _writeRepo.SaveWipAdjustmentAsync(session, companyCd, update, userId));
    }

    private Task<ProductionCostAllocationWriteResponse> PreviewAllocationFromDatabaseAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName)
    {
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;

        return ProductionCostWriteSession.ExecuteRolledBackAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                var source = await _writeRepo.LoadAllocationDryRunFromDbAsync(
                    session, companyCd, runId, runVersion, locked, userId);
                if (!AllocationSourceProvenance.IsAuthoritative(source))
                {
                    throw new InvalidOperationException(
                        "Preview B2 không có pool/basis authoritative từ DB. Không trả số liệu mặc định.");
                }

                var config = AllocationRunConfig.FromLockedRun(locked);
                var result = RunAllocationWithConfig(companyCd, source, config);
                return new ProductionCostAllocationWriteResponse
                {
                    WriteEnabled = _gate.IsWriteEnabled,
                    Persisted = false,
                    PeriodYm = config.PeriodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    AllocateType = config.AllocateType,
                    ByDepartment = config.ByDepartment,
                    Pool = result.Pool,
                    Details = result.Details,
                    Total622 = result.Total622,
                    Total627 = result.Total627,
                    Message = "Preview phân bổ từ sổ cái và RUN trên DB. Chưa Confirm — transaction đã rollback."
                };
            });
    }

    private Task<ProductionCostWipWriteResponse> PreviewCostCalcFromDatabaseAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName)
    {
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;

        return ProductionCostWriteSession.ExecuteRolledBackAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                var source = await _writeRepo.LoadWipDryRunFromDbAsync(
                    session, companyCd, runId, runVersion, locked, userId);
                if (!WipSourceProvenance.IsAuthoritative(source))
                {
                    throw new InvalidOperationException(
                        "Preview B4 không có nguồn giá thành authoritative từ DB.");
                }

                var config = WipRunConfig.FromLockedRun(locked);
                var sources = CostSourceRules.FilterForIncurred(source.Sources).ToList();
                CostSourceRules.AssertNoTransferInIncurred(sources);
                var items = WipCostEngine.Run(
                    source.Products.Select(ClearAmounts).ToList(),
                    config.WipMethod,
                    config.ToEngineConfig(),
                    sources);
                if (items.Any(i => i.HasErrors))
                    throw new InvalidOperationException(items.First(i => i.HasErrors).Errors[0]);

                var receipts = ReceiptPricePrep.Build(items, source.Receipts, companyCd);
                return new ProductionCostWipWriteResponse
                {
                    WriteEnabled = _gate.IsWriteEnabled,
                    Persisted = false,
                    PeriodYm = config.PeriodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    WipMethod = config.WipMethod,
                    IncompleteMode = config.IncompleteMode,
                    Items = items,
                    ReceiptPreps = receipts,
                    TotalCostPriceSum = items.Sum(i => i.TotalCostPrice),
                    InventoryPriceApplied = false,
                    Cogs632Posted = false,
                    Message = "Preview giá thành từ DB. Chưa ghi snapshot — transaction đã rollback."
                };
            });
    }

    private Task<ProductionCostValuationWriteResponse> PreviewValuationFromDatabaseAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName)
    {
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;

        return ProductionCostWriteSession.ExecuteRolledBackAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                var source = await _writeRepo.LoadValuationDryRunFromDbAsync(
                    session, companyCd, runId, runVersion, locked, userId);
                if (!ValuationSourceProvenance.IsAuthoritative(source))
                {
                    throw new InvalidOperationException(
                        "Preview B5 không có receipt/movement/WIP authoritative từ DB.");
                }

                request.ValuationDryRun = source;
                var response = RunDryValuation(companyCd, request);
                response.WriteEnabled = _gate.IsWriteEnabled;
                response.Persisted = false;
                response.PeriodYm = locked.PeriodYm.Trim();
                response.RunId = runId;
                response.RunVersion = runVersion;
                response.InventoryPriceApplied = false;
                response.CogsVoucherPosted = false;
                response.StepStatusesAreClientSimulated = false;
                response.Message = "Preview định giá từ DB. Chưa cập nhật kho và chưa post 632.";
                return response;
            });
    }
}
