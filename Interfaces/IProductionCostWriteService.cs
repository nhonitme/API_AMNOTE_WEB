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
}
