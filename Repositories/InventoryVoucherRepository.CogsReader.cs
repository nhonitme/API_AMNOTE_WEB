using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services.Inventory;
using System.Data;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed partial class InventoryVoucherRepository
    {
        private sealed class CogsContextRow
        {
            public long INVENTORY_ID { get; set; }
            public long? CHIT_ID_COGS { get; set; }
            public long? CHITDETAIL_ID_COGS { get; set; }
            public long CHIT_ID { get; set; }
            public string CHIT_CD { get; set; } = string.Empty;
            public string CHIT_NO { get; set; } = string.Empty;
            public string CHIT_YMD { get; set; } = string.Empty;
            public decimal AMOUNT { get; set; }
            public string IS_LOCK { get; set; } = string.Empty;
            public bool HEADER_VALID { get; set; }
        }

        private sealed class CogsTotalRow
        {
            public long CHIT_ID { get; set; }
            public int DETAIL_COUNT { get; set; }
            public decimal PERSISTED_TOTAL { get; set; }
        }

        private sealed class CogsDetailRow
        {
            public long INVENTORY_ID { get; set; }
            public bool IS_LEGACY { get; set; }
            public long OUTPUT_ID { get; set; }
            public long CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
            public string DEBIT { get; set; } = string.Empty;
            public string CREDIT { get; set; } = string.Empty;
            public decimal AMOUNT { get; set; }
        }

        private static async Task<(List<CogsContextRow> Context, List<CogsTotalRow> Totals,
            List<CogsDetailRow> Details)> ReadCogsBatchRowsAsync(DapperSession session, string sql, object parameters)
        {
            using var grid = await session.QueryMultipleAsync(sql, parameters);
            var context = (await grid.ReadAsync<CogsContextRow>()).ToList();
            var totals = (await grid.ReadAsync<CogsTotalRow>()).ToList();
            var details = (await grid.ReadAsync<CogsDetailRow>()).ToList();
            return (context, totals, details);
        }

        private static async Task<(List<CogsContextRow> Context, List<CogsTotalRow> Totals,
            List<CogsDetailRow> Details, List<InventoryOutput> Outputs)> ReadCogsForUpdateRowsAsync(
            DapperSession session, string companyCd, long transferId)
        {
            using var grid = await session.QueryMultipleAsync(
                "CALL getInventoryCogsForUpdate(@p_COMPANY_CD, @p_INVENTORY_ID)",
                new { p_COMPANY_CD = companyCd, p_INVENTORY_ID = transferId });
            var context = (await grid.ReadAsync<CogsContextRow>()).ToList();
            var totals = (await grid.ReadAsync<CogsTotalRow>()).ToList();
            var details = (await grid.ReadAsync<CogsDetailRow>()).ToList();
            var outputs = (await grid.ReadAsync<InventoryOutput>()).ToList();
            return (context, totals, details, outputs);
        }

        private async Task PopulateInventoryCogsAsync(string companyCd, List<InventoryVoucherDto> vouchers)
        {
            // A stable snapshot across all three result sets, without FOR UPDATE on the list path.
            using var connection = _db.GetOpenConnection(Net_DB.Net_DB_Company);
            await using var session = new DapperSession(connection,
                connection.BeginTransaction(IsolationLevel.RepeatableRead), _db);
            var rows = await ReadCogsBatchRowsAsync(session,
                "CALL getInventoryCogsBatch(@p_COMPANY_CD, @p_INVENTORY_IDS)",
                new { p_COMPANY_CD = companyCd,
                    p_INVENTORY_IDS = string.Join(',', vouchers.Select(x => x.CHIT_ID).Where(x => x > 0).Distinct()) });
            var context = rows.Context.ToLookup(x => x.INVENTORY_ID);
            var totals = rows.Totals.ToDictionary(x => x.CHIT_ID);
            var details = rows.Details.ToLookup(x => x.INVENTORY_ID);
            foreach (var voucher in vouchers)
                voucher.COGS = MapCogs(voucher.CHIT_ID, voucher.OUTPUTS, context, totals, details);
            session.Commit();
        }

        private static InventoryCogsDto? MapCogs(long transferId, IReadOnlyList<InventoryOutput> outputs,
            ILookup<long, CogsContextRow> contexts, IReadOnlyDictionary<long, CogsTotalRow> totals,
            ILookup<long, CogsDetailRow> details)
        {
            var candidates = contexts[transferId].ToList();
            if (candidates.Count == 1 && !candidates[0].CHIT_ID_COGS.HasValue) return null;
            if (candidates.Count != 1 || !candidates[0].HEADER_VALID)
                throw new InvalidOperationException($"Issue voucher {transferId}: missing or invalid COGS header/link.");
            var row = candidates[0];
            totals.TryGetValue(row.CHIT_ID, out var total);
            var count = total?.DETAIL_COUNT ?? 0;
            if ((total?.PERSISTED_TOTAL ?? 0) != row.AMOUNT)
                throw new InvalidOperationException($"Issue voucher {transferId}: OT total does not match its active details.");
            var legacy = !row.CHITDETAIL_ID_COGS.HasValue && count > 1;
            var header = new InventoryCogsDto
            {
                CHIT_ID = row.CHIT_ID, CHIT_CD = row.CHIT_CD, CHIT_NO = row.CHIT_NO,
                CHIT_YMD = row.CHIT_YMD, AMOUNT = row.AMOUNT, IS_LOCK = row.IS_LOCK,
                DETAILS = details[transferId].Where(d => d.IS_LEGACY == legacy &&
                    (legacy || d.CHITDETAIL_ID == row.CHITDETAIL_ID_COGS ||
                        (!row.CHITDETAIL_ID_COGS.HasValue && count == 1)))
                    .Select(d => new InventoryCogsDetailDto
                    {
                        OUTPUT_ID = d.OUTPUT_ID, CHITDETAIL_ID = d.CHITDETAIL_ID,
                        CHITDETAIL_CD = d.CHITDETAIL_CD, DEBIT = d.DEBIT, CREDIT = d.CREDIT, AMOUNT = d.AMOUNT
                    }).ToList()
            };
            if (legacy) InventoryCogsValidation.ValidateLegacy(transferId, header, outputs, count);
            else
            {
                InventoryCogsValidation.Validate(transferId, header, outputs, count);
                header.AMOUNT = header.DETAILS[0].AMOUNT;
            }
            return header;
        }
    }
}
