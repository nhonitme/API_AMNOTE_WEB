using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

/// <summary>
/// Đọc dữ liệu G-K (production_cost_*). Chỉ SELECT — không CALL SP ghi.
/// </summary>
public interface IProductionCostRepository
{
    Task<ProductionCostSchemaProbe> ProbeSchemaAsync(string? databaseName = null);

    Task<ProductionCostPeriodDto?> GetPeriodRowAsync(string companyCd, string periodYm, string? databaseName = null);

    Task<ProductionCostRunDto?> GetRunAsync(string companyCd, long runId, string? databaseName = null);

    Task<ProductionCostRunDto?> GetCurrentRunAsync(string companyCd, string periodYm, string? databaseName = null);

    Task<ProductionCostRunDto?> ResolveRunAsync(
        string companyCd,
        string periodYm,
        long? runId,
        int? runVersion,
        string? databaseName = null);

    /// <summary>true nếu version có ít nhất một dòng detail (pool/detail/wip/source/receipt/nvl).</summary>
    Task<bool> HasRunVersionDetailAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostStepDto>> GetGkStepsAsync(string companyCd, long runId, string? databaseName = null);

    Task<IReadOnlyDictionary<string, string>> GetBiStepStatusesAsync(string companyCd, string periodYm, string? databaseName = null);

    Task<string?> GetPreviousConfirmedSummaryAsync(string companyCd, long runId, int stepIndex, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostObjectDto>> GetObjectsAsync(string companyCd, long runId, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostAllocPoolDto>> GetAllocPoolAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostNvlBasisDto>> GetNvlBasisAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostNvlBasisLineDto>> GetNvlBasisLinesAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostNvlOrphanDto>> GetNvlOrphansAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostAllocDetailDto>> GetAllocDetailAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostWipDto>> GetWipAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostSourceDto>> GetSourcesAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostReceiptDto>> GetReceiptsAsync(string companyCd, long runId, int runVersion, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostHistoryDto>> GetHistoryAsync(string companyCd, string periodYm, long? runId, string? databaseName = null);

    Task<IReadOnlyList<ProductionCostComparisonLineDto>> GetComparisonLinesAsync(
        string companyCd,
        long baseRunId,
        int baseRunVersion,
        long compareRunId,
        int compareRunVersion,
        string? databaseName = null);
}
