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
            var id = await session.QuerySingleAsync<long>(@"
                SELECT INVENTORY_ID FROM chit_inventory_info
                WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND CHIT_TYPE='IO' AND ISDEL='0' FOR UPDATE",
                new { companyCd, transferId });
            if (id != transferId) throw new InvalidOperationException($"Invalid issue voucher {transferId}.");
        }

        private static async Task<List<InventoryOutput>> ReadIssueOutputsAsync(DapperSession session, string companyCd, long transferId)
            => (await session.QueryAsync<InventoryOutput>(@"
                SELECT * FROM chit_inventory_output
                WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND ISDEL='0' ORDER BY SORT,OUTPUT_ID",
                new { companyCd, transferId })).ToList();

        private static async Task EnsureCogsPeriodOpenAsync(DapperSession session, string companyCd, string? ymd)
        {
            if (ymd == null || ymd.Length != 8)
                throw new ArgumentException("CHIT_YMD is required for inventory COGS.");
            var statuses = await session.QueryAsync<string>(@"
                SELECT STATUS FROM acc_period_lock_month WHERE COMPANY_CD=@companyCd AND PERIOD_YM=@period AND ISDEL='0' FOR UPDATE",
                new { companyCd, period = ymd[..6] });
            if (statuses.Any(x => x != "OPEN"))
                throw new InvalidOperationException($"COGS period {ymd[..6]} is not open.");
            var steps = await session.QueryAsync<string>(@"
                SELECT STATUS FROM acc_period_lock_step WHERE COMPANY_CD=@companyCd AND PERIOD_YM=@period
                AND STEP_CODE IN ('COGS_SUMMARY','PROFIT_LOSS_REPORT') FOR UPDATE", new { companyCd, period = ymd[..6] });
            if (steps.Any(x => x == "DONE" || x == "PROCESSING"))
                throw new InvalidOperationException($"COGS period {ymd[..6]} is locked or processing.");
        }

        private static void EnsureCogsUnlocked(InventoryCogsDto cogs, long transferId)
        {
            if (cogs.IS_LOCK != "0")
                throw new InvalidOperationException($"COGS for issue voucher {transferId} is locked.");
        }

        private static async Task<(string Debit, string Credit)> ResolveCogsAccountsAsync(
            DapperSession session, string companyCd)
        {
            var accountCount = await session.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM acclist_info
                WHERE COMPANY_CD=@companyCd AND ACC_CD IN ('632','156') AND ISDEL='0'",
                new { companyCd });
            if (accountCount != 2)
                throw new ArgumentException("Cost accounts 632 and 156 must exist for the company.");
            return ("632", "156");
        }

        private static async Task<InventoryCogsDto?> ReadInventoryCogsAsync(
            DapperSession session, string companyCd, long transferId, IReadOnlyList<InventoryOutput> outputs)
        {
            var headers = (await session.QueryAsync<InventoryCogsDto>(@"
                SELECT h.CHIT_ID,h.CHIT_CD,h.CHIT_NO,h.CHIT_YMD,h.AMOUNT,e.IS_LOCK
                FROM chit_inventory_info t
                INNER JOIN chitinfo h ON h.CHIT_ID=t.CHIT_ID_COGS AND h.COMPANY_CD=t.COMPANY_CD AND h.ISDEL='0'
                    AND h.CHIT_TYPE IN ('OT','IO_COGS') AND h.INPUT_TYPE='AR'
                INNER JOIN chitinfo_ext e ON e.CHIT_ID=h.CHIT_ID AND e.COMPANY_CD=h.COMPANY_CD AND e.ISDEL='0'
                WHERE t.COMPANY_CD=@companyCd AND t.INVENTORY_ID=@transferId
                    AND t.ISDEL='0' AND t.CHIT_TYPE='IO' AND t.INVENTORY_YMD=h.CHIT_YMD FOR UPDATE",
                new { companyCd, transferId })).ToList();
            if (headers.Count == 0)
            {
                var cogsId = await session.QuerySingleAsync<long?>(@"
                    SELECT CHIT_ID_COGS FROM chit_inventory_info
                    WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND ISDEL='0' AND CHIT_TYPE='IO'",
                    new { companyCd, transferId });
                if (!cogsId.HasValue) return null;
            }
            if (headers.Count != 1)
                throw new InvalidOperationException($"Issue voucher {transferId}: missing or invalid COGS header/link.");
            var header = headers[0];
            var detailCount = await session.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM chitdetailinfo WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0'",
                new { companyCd, chitId = header.CHIT_ID });
            var linkedDetailCount = await session.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM chit_inventory_output WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId
                    AND CHITDETAIL_ID_COGS IN (SELECT CHITDETAIL_ID FROM chitdetailinfo WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0')
                    AND ISDEL='0'",
                new { companyCd, transferId, chitId = header.CHIT_ID });
            header.DETAILS = detailCount == 1 && linkedDetailCount == 0
                ? (await session.QueryAsync<InventoryCogsDetailDto>(@"
                SELECT 0 AS OUTPUT_ID,d.CHITDETAIL_ID,d.CHITDETAIL_CD,d.DEBIT,d.CREDIT,d.AMOUNT
                FROM chitdetailinfo d
                INNER JOIN chitdetailinfo_ext e ON e.CHITDETAIL_ID=d.CHITDETAIL_ID AND e.COMPANY_CD=d.COMPANY_CD AND e.ISDEL='0'
                INNER JOIN acclist_info debit_acc ON debit_acc.COMPANY_CD=d.COMPANY_CD AND debit_acc.ACC_CD=d.DEBIT AND debit_acc.ISDEL='0'
                INNER JOIN acclist_info credit_acc ON credit_acc.COMPANY_CD=d.COMPANY_CD AND credit_acc.ACC_CD=d.CREDIT AND credit_acc.ISDEL='0'
                WHERE d.COMPANY_CD=@companyCd AND d.CHIT_ID=@chitId AND d.CHIT_YMD=@ymd AND d.ISDEL='0' LIMIT 1",
                new { companyCd, chitId = header.CHIT_ID, ymd = header.CHIT_YMD })).ToList()
                : (await session.QueryAsync<InventoryCogsDetailDto>(@"
                SELECT o.OUTPUT_ID,d.CHITDETAIL_ID,d.CHITDETAIL_CD,d.DEBIT,d.CREDIT,d.AMOUNT
                FROM chit_inventory_output o
                INNER JOIN chitdetailinfo d ON d.CHITDETAIL_ID=o.CHITDETAIL_ID_COGS AND d.COMPANY_CD=o.COMPANY_CD
                    AND d.CHIT_ID=@chitId AND d.ISDEL='0' AND d.CHIT_YMD=@ymd
                INNER JOIN chitdetailinfo_ext e ON e.CHITDETAIL_ID=d.CHITDETAIL_ID AND e.COMPANY_CD=d.COMPANY_CD AND e.ISDEL='0'
                INNER JOIN acclist_info debit_acc ON debit_acc.COMPANY_CD=d.COMPANY_CD AND debit_acc.ACC_CD=d.DEBIT AND debit_acc.ISDEL='0'
                INNER JOIN acclist_info credit_acc ON credit_acc.COMPANY_CD=d.COMPANY_CD AND credit_acc.ACC_CD=d.CREDIT AND credit_acc.ISDEL='0'
                WHERE o.COMPANY_CD=@companyCd AND o.INVENTORY_ID=@transferId AND o.ISDEL='0' AND o.CHIT_TYPE='IO' FOR UPDATE",
                new { companyCd, transferId, chitId = header.CHIT_ID, ymd = header.CHIT_YMD })).ToList();
            InventoryCogsValidation.Validate(transferId, header, outputs, detailCount);
            return header;
        }

        private static async Task<InventoryCogsDto> SaveInventoryCogsAsync(DapperSession session, string companyCd, string userId,
            long transferId, string voucherNo, string ymd, IReadOnlyList<InventoryOutput> outputs, decimal? cogsAmount, InventoryCogsDto? existing)
        {
            var total = cogsAmount ?? outputs.Sum(x => Math.Round(x.AMOUNT_CC, 2, MidpointRounding.AwayFromZero));
            var accounts = await ResolveCogsAccountsAsync(session, companyCd);
            long chitId;
            string chitCd;
            if (existing == null)
            {
                chitCd = Common.GenerateKeyCd("C");
                await session.ExecuteAsync(@"
                    INSERT INTO chitinfo (COMPANY_CD,CHIT_CD,CHIT_NO,CHIT_YMD,CHIT_TYPE,INPUT_TYPE,AMOUNT,CREATE_BY,UPDATE_BY)
                    VALUES (@companyCd,@chitCd,@voucherNo,@ymd,'OT','AR',@total,@userId,@userId)",
                    new { companyCd, chitCd, voucherNo, ymd, total, userId });
                chitId = await session.QuerySingleAsync<long>("SELECT LAST_INSERT_ID()");
                await session.ExecuteAsync(@"
                    INSERT INTO chitinfo_ext (COMPANY_CD,CHIT_ID,CHIT_CD,CREATE_BY,UPDATE_BY)
                    VALUES (@companyCd,@chitId,@chitCd,@userId,@userId);
                    UPDATE chit_inventory_info SET CHIT_ID_COGS=@chitId
                    WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND CHIT_ID_COGS IS NULL AND ISDEL='0'",
                    new { companyCd, chitId, chitCd, userId, transferId });
            }
            else
            {
                chitId = existing.CHIT_ID;
                chitCd = existing.CHIT_CD;
                await session.ExecuteAsync(@"
                    UPDATE chitinfo SET CHIT_TYPE='OT',INPUT_TYPE='AR',CHIT_NO=@voucherNo,CHIT_YMD=@ymd,AMOUNT=@total,UPDATE_BY=@userId
                    WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0'",
                    new { companyCd, chitId, voucherNo, ymd, total, userId });
            }
            var existingDetails = await session.QueryAsync<long>(@"
                SELECT CHITDETAIL_ID FROM chitdetailinfo
                WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0' ORDER BY SORT,CHITDETAIL_ID",
                new { companyCd, chitId });
            var detailIds = existingDetails.ToList();
            var detailId = detailIds.FirstOrDefault();
            var detailCd = detailId > 0
                ? (await session.QueryAsync<string>(@"SELECT CHITDETAIL_CD FROM chitdetailinfo WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0'", new { companyCd, detailId })).FirstOrDefault() ?? Common.GenerateKeyCd("D")
                : Common.GenerateKeyCd("D");
            if (detailId <= 0)
            {
                await session.ExecuteAsync(@"
                    INSERT INTO chitdetailinfo (COMPANY_CD,CHIT_ID,CHIT_CD,CHITDETAIL_CD,CHIT_YMD,DEBIT,CREDIT,AMOUNT,FC_TYPE,SORT,CREATE_BY,UPDATE_BY)
                    VALUES (@companyCd,@chitId,@chitCd,@detailCd,@ymd,@debit,@credit,@total,'VND',1,@userId,@userId)",
                    new { companyCd, chitId, chitCd, detailCd, ymd, total, userId, debit = accounts.Debit, credit = accounts.Credit });
                detailId = await session.QuerySingleAsync<long>("SELECT LAST_INSERT_ID()");
                await session.ExecuteAsync(@"
                    INSERT INTO chitdetailinfo_ext (COMPANY_CD,CHITDETAIL_ID,CHITDETAIL_CD,HASINVENTORY,INVENTORY_YMD,CREATE_BY,UPDATE_BY)
                    VALUES (@companyCd,@detailId,@detailCd,'0',@ymd,@userId,@userId)",
                    new { companyCd, detailId, detailCd, ymd, userId });
            }
            else
            {
                await session.ExecuteAsync(@"
                    UPDATE chitdetailinfo SET CHIT_YMD=@ymd,AMOUNT=@total,SORT=1,UPDATE_BY=@userId,DEBIT=@debit,CREDIT=@credit
                    WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0';
                    UPDATE chitdetailinfo_ext SET INVENTORY_YMD=@ymd,UPDATE_BY=@userId
                    WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0'",
                    new { companyCd, detailId, ymd, total, userId, debit = accounts.Debit, credit = accounts.Credit });
            }
            foreach (var obsoleteDetailId in detailIds.Where(id => id != detailId))
                await SoftDeleteCogsDetailAsync(session, companyCd, userId, obsoleteDetailId);
            await session.ExecuteAsync(@"
                UPDATE chit_inventory_output SET CHITDETAIL_ID_COGS=NULL
                WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND ISDEL='0'",
                new { companyCd, transferId });
            await session.ExecuteAsync(@"
                DELETE FROM chitdetaildescriptioninfo WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId;
                INSERT INTO chitdetaildescriptioninfo (COMPANY_CD,CHITDETAIL_ID,CHITDETAIL_CD,DESC_CD,LANG_TYPE,DESCRIPTION,CREATE_BY,UPDATE_BY)
                VALUES (@companyCd,@detailId,@detailCd,@detailCd,'VIET',@summary,@userId,@userId)",
                new { companyCd, detailId, detailCd, summary = $"Giá vốn tổng hợp phiếu xuất kho {voucherNo}", userId });
            return await ReadInventoryCogsAsync(session, companyCd, transferId, await ReadIssueOutputsAsync(session, companyCd, transferId));
        }

        private static Task<int> SoftDeleteCogsDetailAsync(DapperSession session, string companyCd, string userId, long detailId)
            => session.ExecuteAsync(@"
                UPDATE chitdetailinfo SET ISDEL='1',UPDATE_BY=@userId WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0';
                UPDATE chitdetailinfo_ext SET ISDEL='1',UPDATE_BY=@userId WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0';
                UPDATE chitdetaildescriptioninfo SET ISDEL='1',UPDATE_BY=@userId WHERE COMPANY_CD=@companyCd AND CHITDETAIL_ID=@detailId AND ISDEL='0'",
                new { companyCd, userId, detailId });

        private static async Task DeleteInventoryCogsAsync(DapperSession session, string companyCd, string userId, long chitId)
        {
            var ids = await session.QueryAsync<long>("SELECT CHITDETAIL_ID FROM chitdetailinfo WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0'", new { companyCd, chitId });
            foreach (var id in ids) await SoftDeleteCogsDetailAsync(session, companyCd, userId, id);
            await session.ExecuteAsync(@"
                UPDATE chitinfo SET ISDEL='1',UPDATE_BY=@userId WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0';
                UPDATE chitinfo_ext SET ISDEL='1',UPDATE_BY=@userId WHERE COMPANY_CD=@companyCd AND CHIT_ID=@chitId AND ISDEL='0'",
                new { companyCd, userId, chitId });
        }

        private static async Task UnlinkInventoryCogsAsync(
            DapperSession session, string companyCd, string userId, long transferId, long cogsChitId)
        {
            await session.ExecuteAsync(@"
                UPDATE chit_inventory_info SET CHIT_ID_COGS=NULL,UPDATE_BY=@userId
                WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND CHIT_TYPE='IO' AND ISDEL='0'
                    AND CHIT_ID_COGS=@cogsChitId;
                UPDATE chit_inventory_output SET CHITDETAIL_ID_COGS=NULL,UPDATE_BY=@userId
                WHERE COMPANY_CD=@companyCd AND INVENTORY_ID=@transferId AND CHIT_TYPE='IO' AND ISDEL='0'",
                new { companyCd, userId, transferId, cogsChitId });
            await DeleteInventoryCogsAsync(session, companyCd, userId, cogsChitId);
        }
    }
}
