using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using System.Linq;

namespace API_AMNOTE_WEB.Repositories
{
    public class GdtImportRepository : IGdtImportRepository
    {
        private readonly DapperExecutor _db;

        public GdtImportRepository(DapperExecutor db)
        {
            _db = db;
        }

        private static (string Header, string Json) Tables(string invoiceType)
        {
            var isBuy = string.Equals(invoiceType, "BUY", StringComparison.OrdinalIgnoreCase);
            return isBuy
                ? ("buy_list_einvoice", "buy_list_json")
                : ("sell_list_einvoice", "sell_list_json");
        }

        public async Task<GdtExistingInvoice> ReadExistingAsync(
            string invoiceType,
            string mhdon,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (headerTable, jsonTable) = Tables(invoiceType);
            const string sqlTemplate = @"
SELECT
    EXISTS(SELECT 1 FROM `{0}` A WHERE A.mhdon = @mhdon AND IFNULL(A.ISDEL,'') != '1' LIMIT 1) AS HasList,
    EXISTS(SELECT 1 FROM `{1}` B WHERE B.mhdon = @mhdon AND IFNULL(B.ISDEL,'') != '1' LIMIT 1) AS HasJson,
    IFNULL((SELECT A.tthai FROM `{0}` A WHERE A.mhdon = @mhdon AND IFNULL(A.ISDEL,'') != '1' LIMIT 1), '') AS Status";

            var sql = string.Format(sqlTemplate, headerTable, jsonTable);
            var rows = await _db.QueryAsync<GdtExistingInvoiceRow>(
                Net_DB.Net_DB_Company,
                sql,
                new { mhdon });
            var row = rows.FirstOrDefault();
            if (row == null)
            {
                return new GdtExistingInvoice();
            }

            return new GdtExistingInvoice
            {
                HasList = row.HasList == 1,
                HasJson = row.HasJson == 1,
                Status = row.Status ?? "",
            };
        }

        public async Task<string?> ReadInvoiceJsonAsync(
            string invoiceType,
            string mhdon,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (_, jsonTable) = Tables(invoiceType);
            var rows = await _db.QueryAsync<string?>(
                Net_DB.Net_DB_Company,
                $@"SELECT json
FROM `{jsonTable}`
WHERE mhdon = @mhdon
  AND IFNULL(ISDEL, '') != '1'
LIMIT 1",
                new { mhdon });
            return rows.FirstOrDefault();
        }

        private sealed class GdtExistingInvoiceRow
        {
            public int HasList { get; set; }
            public int HasJson { get; set; }
            public string? Status { get; set; }
        }

        public async Task InsertListAsync(
            string invoiceType,
            GdtImportInvoiceRow row,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (headerTable, _) = Tables(invoiceType);
            var sql = $@"
INSERT IGNORE INTO `{headerTable}` (
    mhdon, tthai, khmshdon, khhdon, tdlap, nky, shdon, dvtte, mtdtchieu,
    nbten, nbdchi, nbmst, nmten, nmdchi, nmmst,
    tgia, tgtcthue, tgtthue, tgtttbso, tgtphi, ttcktmai, GChu,
    AUTO_CHIT_CD, IS_ATTACH_FILE, ISDEL, USERID
) VALUES (
    @mhdon, @tthai, @khmshdon, @khhdon, @tdlap, @nky, @shdon, @dvtte, @mtdtchieu,
    @nbten, @nbdchi, @nbmst, @nmten, @nmdchi, @nmmst,
    @tgia, @tgtcthue, @tgtthue, @tgtttbso, @tgtphi, @ttcktmai, @GChu,
    @AUTO_CHIT_CD, @IS_ATTACH_FILE, @ISDEL, @USERID
)";
            await _db.ExecuteAsync(Net_DB.Net_DB_Company, sql, row);
        }

        public async Task InvalidateJsonAndUpdateStatusAsync(
            string invoiceType,
            string mhdon,
            string status,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (headerTable, jsonTable) = Tables(invoiceType);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await session.ExecuteAsync(
                    $"DELETE FROM `{jsonTable}` WHERE mhdon = @mhdon",
                    new { mhdon });
                await session.ExecuteAsync(
                    $@"UPDATE `{headerTable}`
SET tthai = @status, ISDEL = ''
WHERE mhdon = @mhdon",
                    new { mhdon, status });
                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task UpsertJsonAsync(
            string invoiceType,
            string mhdon,
            string json,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (_, jsonTable) = Tables(invoiceType);
            var sql = $@"
INSERT INTO `{jsonTable}` (mhdon, json, ISDEL)
VALUES (@mhdon, @json, '0')
ON DUPLICATE KEY UPDATE
    json = VALUES(json),
    ISDEL = '0'";
            await _db.ExecuteAsync(Net_DB.Net_DB_Company, sql, new { mhdon, json });
        }

        public async Task<(int HeaderCount, int JsonCount)> UpsertAsync(
            string invoiceType,
            IReadOnlyList<GdtImportInvoiceRow> rows,
            CancellationToken cancellationToken = default)
        {
            if (rows == null || rows.Count == 0)
            {
                return (0, 0);
            }

            var (headerTable, jsonTable) = Tables(invoiceType);

            var headerSql = $@"
INSERT INTO `{headerTable}` (
    mhdon, tthai, khmshdon, khhdon, tdlap, nky, shdon, dvtte, mtdtchieu,
    nbten, nbdchi, nbmst, nmten, nmdchi, nmmst,
    tgia, tgtcthue, tgtthue, tgtttbso, tgtphi, ttcktmai, GChu,
    AUTO_CHIT_CD, IS_ATTACH_FILE, ISDEL, USERID
) VALUES (
    @mhdon, @tthai, @khmshdon, @khhdon, @tdlap, @nky, @shdon, @dvtte, @mtdtchieu,
    @nbten, @nbdchi, @nbmst, @nmten, @nmdchi, @nmmst,
    @tgia, @tgtcthue, @tgtthue, @tgtttbso, @tgtphi, @ttcktmai, @GChu,
    @AUTO_CHIT_CD, @IS_ATTACH_FILE, @ISDEL, @USERID
)
ON DUPLICATE KEY UPDATE
    tthai = VALUES(tthai),
    khmshdon = VALUES(khmshdon),
    khhdon = VALUES(khhdon),
    tdlap = VALUES(tdlap),
    nky = VALUES(nky),
    shdon = VALUES(shdon),
    dvtte = VALUES(dvtte),
    mtdtchieu = VALUES(mtdtchieu),
    nbten = VALUES(nbten),
    nbdchi = VALUES(nbdchi),
    nbmst = VALUES(nbmst),
    nmten = VALUES(nmten),
    nmdchi = VALUES(nmdchi),
    nmmst = VALUES(nmmst),
    tgia = VALUES(tgia),
    tgtcthue = VALUES(tgtcthue),
    tgtthue = VALUES(tgtthue),
    tgtttbso = VALUES(tgtttbso),
    tgtphi = VALUES(tgtphi),
    ttcktmai = VALUES(ttcktmai),
    GChu = VALUES(GChu),
    AUTO_CHIT_CD = IF(IFNULL(AUTO_CHIT_CD, '') = '', VALUES(AUTO_CHIT_CD), AUTO_CHIT_CD),
    IS_ATTACH_FILE = VALUES(IS_ATTACH_FILE),
    ISDEL = '',
    USERID = VALUES(USERID)";

            var jsonSql = $@"
INSERT INTO `{jsonTable}` (mhdon, json, ISDEL)
VALUES (@mhdon, @json, '0')
ON DUPLICATE KEY UPDATE
    json = VALUES(json),
    ISDEL = '0'";

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var headerCount = 0;
                var jsonCount = 0;

                foreach (var row in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await session.ExecuteAsync(headerSql, row);
                    headerCount += 1;

                    if (row.HasDetailJson && !string.IsNullOrWhiteSpace(row.JsonPayload))
                    {
                        await session.ExecuteAsync(jsonSql, new
                        {
                            mhdon = row.mhdon,
                            json = row.JsonPayload,
                        });
                        jsonCount += 1;
                    }
                }

                session.Commit();
                return (headerCount, jsonCount);
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<GdtSavedLoginDto>> ListSavedLoginsAsync(
            string companyCd,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string sql = @"
SELECT USERNAME AS Username,
       PASSWORD AS Password,
       TOKEN AS Token,
       IFNULL(IS_DEFAULT, '0') AS IsDefault
FROM info_login_tax_office_einvoice
WHERE COMPANY_CD = @companyCd
  AND IFNULL(IS_ACTIVE, '1') = '1'
ORDER BY IS_DEFAULT DESC, USERNAME";
            var rows = await _db.QueryAsync<GdtSavedLoginDto>(
                Net_DB.Net_DB_Manager,
                sql,
                new { companyCd });
            return rows.ToList();
        }

        public async Task SaveLoginAsync(
            string companyCd,
            string username,
            string password,
            string? token = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                await session.ExecuteAsync(
                    @"UPDATE info_login_tax_office_einvoice
SET IS_DEFAULT = '0'
WHERE COMPANY_CD = @companyCd",
                    new { companyCd });

                // Token null/empty → giữ TOKEN cũ; có token mới → ghi đè cột TOKEN.
                await session.ExecuteAsync(
                    @"INSERT INTO info_login_tax_office_einvoice
    (COMPANY_CD, USERNAME, PASSWORD, TOKEN, IS_DEFAULT, IS_ACTIVE)
VALUES
    (@companyCd, @username, @password, @token, '1', '1')
ON DUPLICATE KEY UPDATE
    PASSWORD = VALUES(PASSWORD),
    TOKEN = IF(@token IS NULL OR @token = '', TOKEN, VALUES(TOKEN)),
    IS_DEFAULT = '1',
    IS_ACTIVE = '1'",
                    new { companyCd, username, password, token });

                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<GdtInvoiceVoucherLinkResult>> LinkInvoicesToVouchersAsync(
            string invoiceType,
            IReadOnlyList<GdtInvoiceVoucherLinkItem> items,
            CancellationToken cancellationToken = default)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<GdtInvoiceVoucherLinkResult>();
            }

            var (headerTable, _) = Tables(invoiceType);
            var expectedInputType = string.Equals(invoiceType, "BUY", StringComparison.OrdinalIgnoreCase) ? "AP" : "AR";
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var results = new List<GdtInvoiceVoucherLinkResult>(items.Count);
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mhdon = (item.Mhdon ?? "").Trim();
                    var chitCd = (item.ChitCd ?? "").Trim();
                    var result = new GdtInvoiceVoucherLinkResult { Mhdon = mhdon, ChitCd = chitCd };
                    if (mhdon.Length == 0 || chitCd.Length == 0)
                    {
                        result.Status = "INVALID";
                        result.Error = "Mã hóa đơn và mã chứng từ là bắt buộc.";
                        results.Add(result);
                        continue;
                    }

                    var voucherExists = await session.QueryFirstOrDefaultAsync<long?>(
                        @"SELECT CHIT_ID
FROM chitinfo
WHERE CHIT_CD = @chitCd
  AND INPUT_TYPE = @expectedInputType
  AND IFNULL(ISDEL, '') != '1'
LIMIT 1",
                        new { chitCd, expectedInputType },
                        commandTimeout: 15);
                    if (!voucherExists.HasValue)
                    {
                        result.Status = "VOUCHER_NOT_FOUND";
                        result.Error = "Không tìm thấy chứng từ tương ứng với loại hóa đơn.";
                        results.Add(result);
                        continue;
                    }

                    var currentChitCd = await session.QueryFirstOrDefaultAsync<string>(
                        $"SELECT IFNULL(AUTO_CHIT_CD, '') FROM `{headerTable}` WHERE mhdon = @mhdon AND IFNULL(ISDEL, '') != '1' FOR UPDATE",
                        new { mhdon },
                        commandTimeout: 15);
                    if (currentChitCd == null)
                    {
                        result.Status = "NOT_FOUND";
                        result.Error = "Không tìm thấy hóa đơn hoặc hóa đơn đã bị xóa.";
                    }
                    else if (string.Equals(currentChitCd.Trim(), chitCd, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Status = "ALREADY_LINKED";
                    }
                    else if (currentChitCd.Trim().Length > 0)
                    {
                        result.Status = "CONFLICT";
                        result.Error = "Hóa đơn đã được liên kết với chứng từ khác.";
                    }
                    else
                    {
                        await session.ExecuteAsync(
                            $"UPDATE `{headerTable}` SET AUTO_CHIT_CD = @chitCd WHERE mhdon = @mhdon AND IFNULL(AUTO_CHIT_CD, '') = '' AND IFNULL(ISDEL, '') != '1'",
                            new { mhdon, chitCd },
                            commandTimeout: 15);
                        result.Status = "LINKED";
                    }

                    results.Add(result);
                }

                session.Commit();
                return results;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }
    }
}
