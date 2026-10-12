using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

public interface IServiceAllocationCheckRepository
{
    Task<ServiceAllocationCheckResponse> GetAsync(
        string companyCd,
        string fromYmd,
        string toYmd,
        long? customerId,
        int? allocationFilter,
        string? databaseName = null);
}
