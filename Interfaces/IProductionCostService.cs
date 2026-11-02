using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

public interface IProductionCostService
{
    Task<ProductionCostPeriodDto> GetPeriodAsync(string companyCd, string periodYm, string? databaseName = null);

    Task<ProductionCostStepsResponse> GetStepsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostObjectsResponse> GetObjectsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostAllocPoolResponse> GetAllocPoolAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostAllocBasisResponse> GetAllocBasisAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostAllocDetailResponse> GetAllocDetailAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostWipResponse> GetWipAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostReceiptsResponse> GetReceiptsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostHistoryResponse> GetHistoryAsync(string companyCd, ProductionCostQuery query, string? databaseName = null);

    Task<ProductionCostComparisonResponse> GetComparisonAsync(
        string companyCd,
        string periodYm,
        long? baseRunId,
        int? baseRunVersion,
        long? compareRunId,
        int? compareRunVersion,
        string? databaseName = null);
}
