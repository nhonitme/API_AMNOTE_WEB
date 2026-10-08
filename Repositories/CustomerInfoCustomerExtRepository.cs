using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class CustomerInfoCustomerExtRepository : ICustomerInfoCustomerExtRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;

        public CustomerInfoCustomerExtRepository(DapperExecutor db, IExistenceCheckService existenceCheckService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
        }

        public async Task<IEnumerable<CustomerInfoCustomerExt>> GetCustomerInfoCustomerExtAsync(string companyCd, long? customerId = null, string? customerCd = null)
        {
            const string query = "CALL getCustomerInfoCustomerExt(@p_COMPANY_CD, @p_CUSTOMER_ID, @p_CUSTOMER_CD)";

            return await _db.QueryAsync<CustomerInfoCustomerExt>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CUSTOMER_ID = customerId,
                p_CUSTOMER_CD = customerCd
            });
        }

        public Task<int> SetCustomerInfoCustomerExtAsync(DapperSession session, string companyCd, string userId, CustomerInfoCustomerExtRequest request)
        {
            const string query = @"CALL setCustomerInfoCustomerExt(
                @p_CUSTOMER_ID,
                @p_COMPANY_CD,
                @p_CUSTOMER_CD,
                @p_CATEGORY_CD,
                @p_CUSTOMER_TYPE,
                @p_CUSTOMER_NM_VIET,
                @p_CUSTOMER_NM_ENG,
                @p_CUSTOMER_NM_KOR,
                @p_CUSTOMER_NM_CHINA,
                @p_ADDRESS,
                @p_TEL,
                @p_FAX,
                @p_TAX_CD,
                @p_BANK_ID,
                @p_EMAIL,
                @p_NOTE,
                @p_IDNUMBER,
                @p_BUYER_NM,
                @p_ISDEL,
                @p_USERID)";

            return session.ExecuteAsync(query, new
            {
                p_CUSTOMER_ID = request.CUSTOMER_ID ?? 0,
                p_COMPANY_CD = companyCd,
                p_CUSTOMER_CD = request.CUSTOMER_CD,
                p_CATEGORY_CD = request.CATEGORY_CD,
                p_CUSTOMER_TYPE = request.CUSTOMER_TYPE,
                p_CUSTOMER_NM_VIET = request.CUSTOMER_NM_VIET,
                p_CUSTOMER_NM_ENG = request.CUSTOMER_NM_ENG,
                p_CUSTOMER_NM_KOR = request.CUSTOMER_NM_KOR,
                p_CUSTOMER_NM_CHINA = request.CUSTOMER_NM_CHINA,
                p_ADDRESS = request.ADDRESS,
                p_TEL = request.TEL,
                p_FAX = request.FAX,
                p_TAX_CD = request.TAX_CD,
                p_BANK_ID = request.BANK_ID,
                p_EMAIL = request.EMAIL,
                p_NOTE = request.NOTE,
                p_IDNUMBER = request.IDNUMBER,
                p_BUYER_NM = request.BUYER_NM,
                p_ISDEL = request.ISDEL,
                p_USERID = userId
            });
        }

        public async Task<int> BulkInsertNewCustomersAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<CustomerInfoCustomerExtRequest> records)
        {
            if (records == null || records.Count == 0)
            {
                return 0;
            }

            // Keep packets modest; still ~5 round-trips for ~1000 rows instead of ~2000 SP calls.
            const int chunkSize = 200;
            var inserted = 0;
            for (var offset = 0; offset < records.Count; offset += chunkSize)
            {
                var end = Math.Min(offset + chunkSize, records.Count);
                inserted += await InsertNewCustomerChunkAsync(session, companyCd, userId, records, offset, end);
            }

            return inserted;
        }

        private static async Task<int> InsertNewCustomerChunkAsync(
            DapperSession session,
            string companyCd,
            string userId,
            IReadOnlyList<CustomerInfoCustomerExtRequest> records,
            int startInclusive,
            int endExclusive)
        {
            var count = endExclusive - startInclusive;
            if (count <= 0)
            {
                return 0;
            }

            var infoParams = new DynamicParameters();
            infoParams.Add("p_COMPANY_CD", companyCd);
            infoParams.Add("p_USERID", userId);

            var infoValues = new List<string>(count);
            var codes = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var record = records[startInclusive + i];
                var cd = Common.NormalizeRequiredText(record.CUSTOMER_CD);
                if (string.IsNullOrWhiteSpace(cd))
                {
                    throw new InvalidOperationException("CUSTOMER_CD is required");
                }

                codes.Add(cd);
                infoValues.Add(
                    $"(@p_COMPANY_CD, @cd{i}, @cat{i}, @type{i}, @nmV{i}, @nmE{i}, @nmK{i}, @nmC{i}, @addr{i}, @tel{i}, @isdel{i}, NOW(), @p_USERID, NOW(), @p_USERID)");
                infoParams.Add($"cd{i}", cd);
                infoParams.Add($"cat{i}", record.CATEGORY_CD);
                infoParams.Add($"type{i}", record.CUSTOMER_TYPE);
                infoParams.Add($"nmV{i}", record.CUSTOMER_NM_VIET);
                infoParams.Add($"nmE{i}", record.CUSTOMER_NM_ENG);
                infoParams.Add($"nmK{i}", record.CUSTOMER_NM_KOR);
                infoParams.Add($"nmC{i}", record.CUSTOMER_NM_CHINA);
                infoParams.Add($"addr{i}", record.ADDRESS);
                infoParams.Add($"tel{i}", record.TEL);
                infoParams.Add($"isdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var infoSql = $@"
INSERT INTO customer_info
(
  COMPANY_CD, CUSTOMER_CD, CATEGORY_CD, CUSTOMER_TYPE,
  CUSTOMER_NM_VIET, CUSTOMER_NM_ENG, CUSTOMER_NM_KOR, CUSTOMER_NM_CHINA,
  ADDRESS, TEL, ISDEL, CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
)
VALUES {string.Join(",\n", infoValues)}";

            var infoRows = await session.ExecuteAsync(infoSql, infoParams);
            if (infoRows <= 0)
            {
                return 0;
            }

            var idParams = new DynamicParameters();
            idParams.Add("p_COMPANY_CD", companyCd);
            var inList = new List<string>(count);
            for (var i = 0; i < codes.Count; i++)
            {
                inList.Add($"@idCd{i}");
                idParams.Add($"idCd{i}", codes[i]);
            }

            var idSql = $@"
SELECT CUSTOMER_ID, CUSTOMER_CD
FROM customer_info
WHERE COMPANY_CD = @p_COMPANY_CD
  AND CUSTOMER_CD IN ({string.Join(", ", inList)})
  AND (ISDEL IS NULL OR ISDEL = '0')";

            var idRows = (await session.QueryAsync<(long CUSTOMER_ID, string CUSTOMER_CD)>(idSql, idParams)).ToList();
            var idByCode = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in idRows)
            {
                if (!string.IsNullOrWhiteSpace(row.CUSTOMER_CD))
                {
                    idByCode[row.CUSTOMER_CD] = row.CUSTOMER_ID;
                }
            }

            var extParams = new DynamicParameters();
            extParams.Add("p_COMPANY_CD", companyCd);
            extParams.Add("p_USERID", userId);
            var extValues = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var record = records[startInclusive + i];
                var cd = codes[i];
                if (!idByCode.TryGetValue(cd, out var customerId))
                {
                    throw new InvalidOperationException($"Bulk insert customer '{cd}' did not return CUSTOMER_ID.");
                }

                extValues.Add(
                    $"(@p_COMPANY_CD, @extId{i}, @fax{i}, @tax{i}, @bank{i}, @email{i}, @note{i}, @idn{i}, @buyer{i}, @extIsdel{i}, NOW(), @p_USERID, NOW(), @p_USERID)");
                extParams.Add($"extId{i}", customerId);
                extParams.Add($"fax{i}", record.FAX);
                extParams.Add($"tax{i}", record.TAX_CD);
                extParams.Add($"bank{i}", record.BANK_ID);
                extParams.Add($"email{i}", record.EMAIL);
                extParams.Add($"note{i}", record.NOTE);
                extParams.Add($"idn{i}", record.IDNUMBER);
                extParams.Add($"buyer{i}", record.BUYER_NM);
                extParams.Add($"extIsdel{i}", string.IsNullOrWhiteSpace(record.ISDEL) ? "0" : record.ISDEL);
            }

            var extSql = $@"
INSERT INTO customer_ext
(
  COMPANY_CD, CUSTOMER_ID, FAX, TAX_CD, BANK_ID, EMAIL, NOTE, IDNUMBER, BUYER_NM,
  ISDEL, CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
)
VALUES {string.Join(",\n", extValues)}";

            await session.ExecuteAsync(extSql, extParams);
            return infoRows;
        }

        public Task<int> DeleteCustomerInfoCustomerExtAsync(DapperSession session, string companyCd, long customerId, string userId)
        {
            return session.ExecuteAsync(
                "CALL delCustomerInfoCustomerExt(@p_COMPANY_CD, @p_CUSTOMER_ID, @p_USERID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CUSTOMER_ID = customerId,
                    p_USERID = userId
                });
        }

        public Task<bool> CustomerCdExistsAsync(string companyCd, string customerCd, long? customerId = null, string? databaseName = null)
        {
            return _existenceCheckService.ExistsAsync(
                "customer_info",
                "CUSTOMER_CD",
                customerCd,
                companyCd,
                idField: "CUSTOMER_ID",
                excludeId: customerId,
                dbName: databaseName);
        }
    }
}
