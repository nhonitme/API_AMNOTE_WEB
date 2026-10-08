using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IChitInfoWriteService
    {
        Task<long> CreateAsync(
            string companyCd,
            string inputType,
            string supportedType,
            string userId,
            ChitInfoRequest request,
            IReadOnlyCollection<string>? supportedTypeAliases = null,
            ChitInfoCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null);

        Task<long> UpdateAsync(
            string companyCd,
            string inputType,
            string supportedType,
            string userId,
            ChitInfo existing,
            ChitInfoRequest request,
            IReadOnlyCollection<string>? supportedTypeAliases = null,
            ChitInfoCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null);
    }

    public sealed class ChitInfoCodeMaps
    {
        public IReadOnlyDictionary<string, long>? BankIdsByCode { get; init; }
        public IReadOnlyDictionary<string, long>? CustomerIdsByCode { get; init; }
        public IReadOnlyDictionary<string, long>? DepartmentIdsByCode { get; init; }
    }
}
