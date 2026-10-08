using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public partial class ChitInfoRepository
    {
        public async Task<IReadOnlyList<InventoryInput>> GetInventoryInputsByChitDetailIdsAsync(string companyCd, IEnumerable<long> chitDetailIds, string? databaseName = null)
        {
            var ids = chitDetailIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
            {
                return new List<InventoryInput>();
            }

            const string proc = "CALL getChitInventoryInput(@p_COMPANY_CD, @p_CHITDETAIL_IDS)";
            var items = await _db.QueryAsync<InventoryInput>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHITDETAIL_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

        public async Task<IReadOnlyList<InventoryOutput>> GetInventoryOutputsByChitDetailIdsAsync(string companyCd, IEnumerable<long> chitDetailIds, string? databaseName = null)
        {
            var ids = chitDetailIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
            {
                return new List<InventoryOutput>();
            }

            const string proc = "CALL getChitInventoryOutput(@p_COMPANY_CD, @p_CHITDETAIL_IDS)";
            var items = await _db.QueryAsync<InventoryOutput>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHITDETAIL_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

        public async Task<IReadOnlyList<InventoryInput>> GetInventoryInputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null)
        {
            var ids = chitIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
            {
                return new List<InventoryInput>();
            }

            const string proc = "CALL getChitInventoryInputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)";
            var items = await _db.QueryAsync<InventoryInput>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

        public async Task<IReadOnlyList<InventoryOutput>> GetInventoryOutputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null)
        {
            var ids = chitIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
            {
                return new List<InventoryOutput>();
            }

            const string proc = "CALL getChitInventoryOutputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)";
            var items = await _db.QueryAsync<InventoryOutput>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

    }
}
