using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories;

public sealed class ServiceAllocationCheckRepository : IServiceAllocationCheckRepository
{
    private readonly DapperExecutor _db;

    public ServiceAllocationCheckRepository(DapperExecutor db)
    {
        _db = db;
    }

    public async Task<ServiceAllocationCheckResponse> GetAsync(
        string companyCd,
        string fromYmd,
        string toYmd,
        long? customerId,
        int? allocationFilter,
        string? databaseName = null)
    {
        // UI: null = all, 0 = allocated, 1 = not allocated.
        // Existing stored procedure: null = all, 1 = allocated, 0 = not allocated.
        // Preserve the stored procedure contract for other existing consumers.
        int? procedureFilter = allocationFilter switch
        {
            0 => 1,
            1 => 0,
            _ => null
        };

        const string query =
            "CALL sp_chit_inventory_service_get(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, @p_CUSTOMER_ID, @p_ALLOCATION_FILTER)";

        var (masters, details) = await _db.QueryMultipleAsync<
            ServiceAllocationCheckRow,
            ServiceAllocationCheckDetailRow>(
            Net_DB.Net_DB_Company,
            query,
            new
            {
                p_COMPANY_CD = companyCd,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_CUSTOMER_ID = customerId is > 0 ? customerId : null,
                p_ALLOCATION_FILTER = procedureFilter
            },
            databaseName);

        // The legacy procedure returns all company allocation details in result set #2.
        // Return only allocations belonging to the filtered master records.
        var detailGroups = details
            .GroupBy(item => item.SERVICES_CHIT_ID)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var master in masters)
        {
            master.ALLOCATIONS = detailGroups.TryGetValue(master.SERVICES_CHIT_ID, out var rows)
                ? rows
                : new List<ServiceAllocationCheckDetailRow>();
        }

        return new ServiceAllocationCheckResponse
        {
            ITEMS = masters,
            TOTAL_RECORDS = masters.Count
        };
    }
}
