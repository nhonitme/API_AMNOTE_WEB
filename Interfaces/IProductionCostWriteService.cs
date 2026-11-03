using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

/// <summary>Ghi G-K Bước 2 / 3 / 4 — bị chặn mặc định bởi write gate.</summary>
public interface IProductionCostWriteService
{
    Task<ProductionCostAllocationWriteResponse> PreviewAllocationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostAllocationWriteResponse> ConfirmAllocationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostTransferWriteResponse> PreviewTransferAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostTransferWriteResponse> ConfirmTransferAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostWipWriteResponse> PreviewCostCalcAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostWipWriteResponse> ConfirmCostCalcAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostValuationWriteResponse> PreviewValuationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostValuationWriteResponse> ConfirmValuationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostLifecycleWriteResponse> PreviewReopenAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostLifecycleWriteResponse> ConfirmReopenAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostLifecycleWriteResponse> PreviewRecalculateAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);

    Task<ProductionCostEnsureResult> EnsureDraftRunAsync(
        string companyCd, string? userId, string periodYm, string? databaseName = null);

    Task UpdateRunConfigAsync(
        string companyCd, string? userId, ProductionCostRunConfigUpdate update, string? databaseName = null);

    Task UpdateObjectSelectionAsync(
        string companyCd, string? userId, ProductionCostObjectSelectionUpdate update, string? databaseName = null);

    Task<int> SyncObjectsFromReceiptsAsync(
        string companyCd, string? userId, long runId, int runVersion, string? databaseName = null);

    Task UpdatePoolReductionAsync(
        string companyCd, string? userId, ProductionCostPoolReductionUpdate update, string? databaseName = null);

    Task SaveWipAdjustmentAsync(
        string companyCd, string? userId, ProductionCostWipAdjustmentUpdate update, string? databaseName = null);

    Task<ProductionCostLifecycleWriteResponse> PreviewCompareAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null);
}
