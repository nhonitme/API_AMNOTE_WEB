using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services.Inventory;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed partial class InventoryVoucherRepository
    {
        private static async Task LockInventoryIssueAsync(DapperSession session, string companyCd, long transferId)
        {
            var id = await session.QuerySingleAsync<long>("CALL getInventoryIssueForUpdate(@companyCd, @transferId)",
                new { companyCd, transferId });
            if (id != transferId) throw new InvalidOperationException($"Invalid issue voucher {transferId}.");
        }

        private static async Task EnsureCogsPeriodOpenAsync(DapperSession session, string companyCd, string? ymd)
        {
            if (ymd == null || ymd.Length != 8)
                throw new ArgumentException("CHIT_YMD is required for inventory COGS.");
            using var grid = await session.QueryMultipleAsync(
                "CALL getInventoryCogsPeriodLockState(@companyCd, @period)", new { companyCd, period = ymd[..6] });
            var statuses = (await grid.ReadAsync<string>()).ToList();
            var steps = (await grid.ReadAsync<string>()).ToList();
            if (statuses.Any(x => x != "OPEN"))
                throw new InvalidOperationException($"COGS period {ymd[..6]} is not open.");
            if (steps.Any(x => x == "DONE" || x == "PROCESSING"))
                throw new InvalidOperationException($"COGS period {ymd[..6]} is locked or processing.");
        }

        private static void EnsureCogsUnlocked(InventoryCogsDto cogs, long transferId)
        {
            if (cogs.IS_LOCK != "0")
                throw new InvalidOperationException($"COGS for issue voucher {transferId} is locked.");
        }

        private static void ThrowCogsResultCode(string? resultCode, long transferId)
        {
            switch (resultCode)
            {
                case "OK":
                case "NO_CHANGE":
                    return;
                case "NEGATIVE_AMOUNT":
                    throw new ArgumentException("COGS amount must not be negative.");
                case "MISSING_ACCOUNTS":
                    throw new ArgumentException("Cost accounts 632 and 156 must exist for the company.");
                case "SHARED_DATE_CHANGE":
                    throw new InvalidOperationException("Cannot change the date of a COGS voucher shared with other details.");
                case "DATE_MISMATCH":
                    throw new InvalidOperationException("The issue date must match its linked OT detail.");
                case "INVALID_DETAIL":
                    throw new InvalidOperationException("Invalid OT COGS detail or date.");
                case "LOCKED":
                    throw new InvalidOperationException($"COGS for issue voucher {transferId} is locked.");
                case "PERIOD_CLOSED":
                    throw new InvalidOperationException("COGS period is not open.");
                case "PERIOD_LOCKED":
                    throw new InvalidOperationException("COGS period is locked or processing.");
                case "LEGACY_LINKS":
                    throw new InvalidOperationException("Migrate historical inventory links on this OT before linking another issue.");
                case "LEGACY_UNLINK":
                    throw new InvalidOperationException("Historical COGS link requires migration before unlinking.");
                case "NOT_FOUND":
                    throw new InvalidOperationException($"Issue voucher {transferId}: missing or invalid COGS header/link.");
                default:
                    throw new InvalidOperationException($"Unexpected COGS result code '{resultCode}' for issue voucher {transferId}.");
            }
        }

        private static async Task<string> ExecuteCogsResultAsync(DapperSession session, string sql, object parameters)
        {
            var resultCode = await session.QuerySingleAsync<string>(sql, parameters);
            return resultCode;
        }

        private static async Task<InventoryCogsDto?> ReadInventoryCogsAsync(
            DapperSession session, string companyCd, long transferId)
        {
            var rows = await ReadCogsForUpdateRowsAsync(session, companyCd, transferId);
            return MapCogs(transferId, rows.Outputs, rows.Context.ToLookup(x => x.INVENTORY_ID),
                rows.Totals.ToDictionary(x => x.CHIT_ID), rows.Details.ToLookup(x => x.INVENTORY_ID));
        }

        private static async Task<InventoryCogsDto> SaveInventoryCogsAsync(DapperSession session, string companyCd, string userId,
            long transferId, string voucherNo, string ymd, IReadOnlyList<InventoryOutput> outputs, decimal? cogsAmount, InventoryCogsDto? existing, long? requestedDetailId)
        {
            var total = Math.Round(cogsAmount ?? outputs.Sum(x => Math.Round(x.AMOUNT_CC, 2, MidpointRounding.AwayFromZero)), 2, MidpointRounding.AwayFromZero);
            if (cogsAmount < 0 || total < 0) throw new ArgumentException("COGS amount must not be negative.");
            if (requestedDetailId is > 0 && existing != null && requestedDetailId != existing.DETAILS.Single().CHITDETAIL_ID)
                throw new InvalidOperationException("Unlink the existing COGS detail before selecting another detail.");
            if (existing == null && requestedDetailId is > 0)
            {
                await AttachInventoryCogsDetailAsync(session, companyCd, userId, transferId, ymd, requestedDetailId.Value);
                return (await ReadInventoryCogsAsync(session, companyCd, transferId))!;
            }

            var resultCode = await ExecuteCogsResultAsync(session,
                "CALL setInventoryCogs(@companyCd, @userId, @transferId, @voucherNo, @ymd, @total, @amountProvided, @chitId, @chitCd, @detailId, @detailCd, @summary)",
                new
                {
                    companyCd,
                    userId,
                    transferId,
                    voucherNo,
                    ymd,
                    total,
                    amountProvided = cogsAmount.HasValue ? "1" : "0",
                    chitId = existing?.CHIT_ID,
                    chitCd = existing?.CHIT_CD ?? Common.GenerateKeyCd("C"),
                    detailId = existing?.DETAILS.Single().CHITDETAIL_ID,
                    detailCd = existing?.DETAILS.Single().CHITDETAIL_CD ?? Common.GenerateKeyCd("D"),
                    summary = $"Giá vốn tổng hợp phiếu xuất kho {voucherNo}"
                });
            ThrowCogsResultCode(resultCode, transferId);
            if (resultCode == "NO_CHANGE")
                return existing!;
            return (await ReadInventoryCogsAsync(session, companyCd, transferId))!;
        }

        private static async Task AttachInventoryCogsDetailAsync(DapperSession session, string companyCd, string userId,
            long transferId, string ymd, long detailId)
        {
            var resultCode = await ExecuteCogsResultAsync(session,
                "CALL attachInventoryCogsDetail(@companyCd, @userId, @transferId, @detailId, @ymd)",
                new { companyCd, userId, transferId, detailId, ymd });
            ThrowCogsResultCode(resultCode, transferId);
        }

        private static async Task UnlinkInventoryCogsAsync(
            DapperSession session, string companyCd, string userId, long transferId, long cogsChitId)
        {
            var resultCode = await ExecuteCogsResultAsync(session,
                "CALL clearInventoryCogsLink(@companyCd, @userId, @transferId, @cogsChitId)",
                new { companyCd, userId, transferId, cogsChitId });
            ThrowCogsResultCode(resultCode, transferId);
        }

    }
}
