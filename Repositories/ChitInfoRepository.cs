using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Dapper;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public partial class ChitInfoRepository : IChitInfoRepository, IInventoryLinkRepository
    {
        private sealed class TotalRecordResult
        {
            public int TOTAL_RECORDS { get; set; }
        }

        private sealed class InventoryOutputLinkRow
        {
            public long OUTPUT_ID { get; set; }
            public long? CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
            public long? LINKED_CHIT_ID { get; set; }
        }

        private sealed class InventoryInputLinkRow
        {
            public long INPUT_ID { get; set; }
            public long? CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
            public long? LINKED_CHIT_ID { get; set; }
        }

        private sealed class InventoryOutputLinkAssignment
        {
            public long OUTPUT_ID { get; set; }
            public long CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
        }

        private sealed class InventoryInputLinkAssignment
        {
            public long INPUT_ID { get; set; }
            public long CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
        }

        private sealed class InventorySourceReferenceRow
        {
            public long CHIT_ID { get; set; }
            public string CHIT_CD { get; set; } = string.Empty;
            public string CHIT_TYPE { get; set; } = string.Empty;
            public long CHITDETAIL_ID { get; set; }
            public string CHITDETAIL_CD { get; set; } = string.Empty;
        }

        private sealed class SourceVoucherRow
        {
            public long CHIT_ID { get; set; }
            public string CHIT_CD { get; set; } = string.Empty;
            public string CHIT_TYPE { get; set; } = string.Empty;
        }

        private sealed class InventoryLinkStatusRow
        {
            public long CHIT_ID { get; set; }
            public string CHIT_CD { get; set; } = string.Empty;
            public string CHIT_TYPE { get; set; } = string.Empty;
            public int SOURCE_TOTAL_QUANTITY { get; set; }
            public int LINKED_TOTAL_QUANTITY { get; set; }
            public int INVENTORY_VOUCHER_COUNT { get; set; }
            public string? LINKED_CHITDETAIL_IDS { get; set; }
        }

        private sealed class InventoryLinkVoucherRow
        {
            public long SOURCE_CHIT_ID { get; set; }
            public long INVENTORY_CHIT_ID { get; set; }
            public string INVENTORY_CHIT_CD { get; set; } = string.Empty;
            public string? INVENTORY_CHIT_NO { get; set; }
            public string? INVENTORY_CHIT_YMD { get; set; }
            public string INVENTORY_CHIT_TYPE { get; set; } = string.Empty;
            public decimal LINKED_QUANTITY { get; set; }
            public int LINKED_LINE_COUNT { get; set; }
        }

        private const int ReadCommandTimeoutSeconds = 60;

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;
        private readonly ISysCodeSequenceRepository _sequenceRepository;

        public ChitInfoRepository(DapperExecutor db, IActivityLogService activityLogService, ISysCodeSequenceRepository sequenceRepository)
        {
            _db = db;
            _activityLogService = activityLogService;
            _sequenceRepository = sequenceRepository;
        }

        public async Task<IEnumerable<ChitInfo>> GetChitInfosAsync(
            string companyCd,
            string inputType,
            string? chitType = null,
            long? chitId = null,
            string? searchText = null,
            string? fromYmd = null,
            string? toYmd = null,
            string? databaseName = null)
        {
            var result = await GetChitInfosPagedAsync(
                companyCd,
                inputType,
                chitType,
                chitId,
                searchText,
                fromYmd,
                toYmd,
                1,
                int.MaxValue,
                databaseName);

            return result.Items;
        }

        public async Task<(IReadOnlyList<ChitInfo> Items, int TotalRecords)> GetChitInfosPagedAsync(
            string companyCd,
            string inputType,
            string? chitType = null,
            long? chitId = null,
            string? searchText = null,
            string? fromYmd = null,
            string? toYmd = null,
            int pageNumber = 1,
            int pageSize = 20,
            string? databaseName = null)
        {
            const string query = "CALL getchitinfo(@p_COMPANY_CD, @p_INPUT_TYPE, @p_CHIT_TYPE, @p_CHIT_ID, @p_SEARCH_TEXT, @p_FROM_YMD, @p_TO_YMD, @p_PAGE_NUMBER, @p_PAGE_SIZE)";

            var (items, totals) = await _db.QueryMultipleAsync<ChitInfo, TotalRecordResult>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_TYPE = inputType,
                    p_CHIT_TYPE = chitType,
                    p_CHIT_ID = chitId,
                    p_SEARCH_TEXT = searchText,
                    p_FROM_YMD = fromYmd,
                    p_TO_YMD = toYmd,
                    p_PAGE_NUMBER = pageNumber,
                    p_PAGE_SIZE = pageSize
                },
                databaseName,
                ReadCommandTimeoutSeconds);

            var totalRow = totals.FirstOrDefault();
            return (items, totalRow?.TOTAL_RECORDS ?? items.Count);
        }

        public async Task<(IReadOnlyList<ChitInfo> Items, int TotalRecords)> GetChitInfosPagedByInputTypesAsync(
            string companyCd,
            IReadOnlyList<string> inputTypes,
            string? chitType = null,
            long? chitId = null,
            string? searchText = null,
            string? fromYmd = null,
            string? toYmd = null,
            int pageNumber = 1,
            int pageSize = 20,
            string? databaseName = null)
        {
            var normalizedInputTypes = (inputTypes ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedInputTypes.Count == 0)
            {
                return (Array.Empty<ChitInfo>(), 0);
            }

            if (normalizedInputTypes.Count == 1)
            {
                return await GetChitInfosPagedAsync(
                    companyCd,
                    normalizedInputTypes[0],
                    chitType,
                    chitId,
                    searchText,
                    fromYmd,
                    toYmd,
                    pageNumber,
                    pageSize,
                    databaseName);
            }

            var offset = Math.Max(pageNumber - 1, 0) * pageSize;
            var inputTypeClause = string.Join(", ", normalizedInputTypes.Select(item => $"'{item.Replace("'", "''")}'"));
            var normalizedSearchText = Common.NormalizeNullableText(searchText);
            var hasSearchText = !string.IsNullOrWhiteSpace(normalizedSearchText);
            var searchPattern = hasSearchText ? $"%{normalizedSearchText.Replace("%", "\\%").Replace("_", "\\_")}%" : null;

            var whereClause = $@"
WHERE h.COMPANY_CD = @p_COMPANY_CD
  AND IFNULL(h.ISDEL, '0') = '0'
  AND h.INPUT_TYPE IN ({inputTypeClause})
  AND (@p_CHIT_TYPE IS NULL OR @p_CHIT_TYPE = '' OR h.CHIT_TYPE = @p_CHIT_TYPE)
  AND (@p_CHIT_ID IS NULL OR h.CHIT_ID = @p_CHIT_ID)
  AND (@p_FROM_YMD IS NULL OR @p_FROM_YMD = '' OR h.CHIT_YMD >= @p_FROM_YMD)
  AND (@p_TO_YMD IS NULL OR @p_TO_YMD = '' OR h.CHIT_YMD <= @p_TO_YMD)
  AND (
        @p_HAS_SEARCH_TEXT = 0
        OR h.CHIT_NO LIKE @p_SEARCH_TEXT
        OR h.CHIT_CD LIKE @p_SEARCH_TEXT
        OR IFNULL(dv.DESCRIPTION, '') LIKE @p_SEARCH_TEXT
      )";

            var countQuery = $@"
SELECT COUNT(*) AS TOTAL_RECORDS
FROM chitinfo h
LEFT JOIN chitdescriptioninfo dv
    ON dv.CHIT_ID = h.CHIT_ID
   AND dv.COMPANY_CD = h.COMPANY_CD
   AND dv.LANG_TYPE = 'VIET'
   AND IFNULL(dv.ISDEL, '0') = '0'
{whereClause};";

            var listQuery = $@"
SELECT
    h.CHIT_ID,
    h.COMPANY_CD,
    h.CHIT_CD,
    h.CHIT_NO,
    h.CHIT_YMD,
    h.CHIT_TYPE,
    h.INPUT_TYPE,
    h.LOCK_STEP_CODE,
    h.AMOUNT,
    h.PAYER_INFO,
    h.ISDEL,
    h.CREATE_BY,
    h.CREATE_AT,
    h.UPDATE_BY,
    h.UPDATE_AT,
    dv.DESCRIPTION AS DESCRIPTION_VIET,
    de.DESCRIPTION AS DESCRIPTION_ENG,
    dk.DESCRIPTION AS DESCRIPTION_KOR,
    (
        SELECT COUNT(*)
        FROM chitdetailinfo cd
        WHERE cd.CHIT_ID = h.CHIT_ID
          AND cd.COMPANY_CD = h.COMPANY_CD
          AND IFNULL(cd.ISDEL, '0') = '0'
    ) AS DETAIL_COUNT
FROM chitinfo h
LEFT JOIN chitdescriptioninfo dv
    ON dv.CHIT_ID = h.CHIT_ID
   AND dv.COMPANY_CD = h.COMPANY_CD
   AND dv.LANG_TYPE = 'VIET'
   AND IFNULL(dv.ISDEL, '0') = '0'
LEFT JOIN chitdescriptioninfo de
    ON de.CHIT_ID = h.CHIT_ID
   AND de.COMPANY_CD = h.COMPANY_CD
   AND de.LANG_TYPE = 'ENG'
   AND IFNULL(de.ISDEL, '0') = '0'
LEFT JOIN chitdescriptioninfo dk
    ON dk.CHIT_ID = h.CHIT_ID
   AND dk.COMPANY_CD = h.COMPANY_CD
   AND dk.LANG_TYPE = 'KOR'
   AND IFNULL(dk.ISDEL, '0') = '0'
{whereClause}
ORDER BY h.CHIT_YMD DESC, h.CHIT_ID DESC
LIMIT @p_OFFSET, @p_PAGE_SIZE;";

            var param = new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_TYPE = chitType,
                p_CHIT_ID = chitId,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_HAS_SEARCH_TEXT = hasSearchText ? 1 : 0,
                p_SEARCH_TEXT = searchPattern,
                p_OFFSET = offset,
                p_PAGE_SIZE = pageSize
            };

            var totalRecords = await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Company,
                countQuery,
                param,
                databaseName);

            var items = (await _db.QueryAsync<ChitInfo>(
                Net_DB.Net_DB_Company,
                listQuery,
                param,
                databaseName,
                ReadCommandTimeoutSeconds)).ToList();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.INPUT_TYPE))
                {
                    item.INPUT_TYPE = normalizedInputTypes[0];
                }
            }

            return (items, totalRecords);
        }

        public async Task<IReadOnlyList<ChitDetail>> GetChitDetailsByChitIdsAsync(string companyCd, string inputType, IEnumerable<long> chitIds, string? databaseName = null)
        {
            var normalizedIds = chitIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return new List<ChitDetail>();
            }

            const string query = "CALL getchitdetail_batch(@p_COMPANY_CD, @p_INPUT_TYPE, @p_CHIT_IDS)";

            var items = await _db.QueryAsync<ChitDetail>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_INPUT_TYPE = inputType,
                p_CHIT_IDS = string.Join(",", normalizedIds)
            }, databaseName);

            return items.ToList();
        }

        public async Task<long> SaveChitInfoAsync(string companyCd, string inputType, string userId, ChitInfoRequest request, string? databaseName = null)
        {
            var existingHeader = request.CHIT_ID.HasValue && request.CHIT_ID.Value > 0
                ? (await GetChitInfosAsync(companyCd, inputType, null, request.CHIT_ID.Value, null, null, null, databaseName)).FirstOrDefault()
                : null;
            var existingDetails = existingHeader == null
                ? new List<ChitDetail>()
                : (await GetChitDetailsByChitIdsAsync(companyCd, inputType, new[] { existingHeader.CHIT_ID }, databaseName)).ToList();
            var oldData = existingHeader == null
                ? string.Empty
                : JsonSerializer.Serialize(new
                {
                    Header = existingHeader,
                    Details = existingDetails
                });

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
            try
            {
                // Fail fast on lock wait (FOR UPDATE in getSysCodeSequenceNext) instead of hanging forever.
                await session.ExecuteAsync("SET SESSION innodb_lock_wait_timeout = 15", commandTimeout: 5);
                const int cmdTimeoutSec = 60;

                var existingDetailMap = existingDetails
                    .Where(detail => detail.CHITDETAIL_ID > 0)
                    .ToDictionary(detail => detail.CHITDETAIL_ID, detail => Common.NormalizeNullableText(detail.CHITDETAIL_CD) ?? string.Empty);

                if (request.CHIT_ID.GetValueOrDefault() <= 0)
                {
                    request.CHIT_CD = Common.GenerateKeyCd("C");
                    var requestedChitNo = Common.NormalizeNullableText(request.CHIT_NO);
                    // Excel/convert imports already carry historical CHIT_NO — skip sys_code_sequence
                    // FOR UPDATE (common hang under concurrent workers / orphaned locks).
                    if (string.Equals(request.ISEXCEL, "1", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(requestedChitNo))
                    {
                        request.CHIT_NO = requestedChitNo;
                    }
                    else
                    {
                        request.CHIT_NO = await CodeSequenceHelper.ResolveRequiredCodeAsync(
                            _sequenceRepository,
                            session,
                            companyCd,
                            Common.NormalizeChitType(request.CHIT_TYPE) ?? request.CHIT_TYPE ?? string.Empty,
                            requestedChitNo,
                            "Chit no",
                            Common.ParseNullableDateTimeText(request.CHIT_YMD));
                    }
                }
                else
                {
                    request.CHIT_CD = existingHeader?.CHIT_CD;
                    var requestedChitNo = Common.NormalizeNullableText(request.CHIT_NO);
                    var existingChitNo = Common.NormalizeNullableText(existingHeader?.CHIT_NO);

                    if (!string.Equals(requestedChitNo, existingChitNo, StringComparison.OrdinalIgnoreCase))
                    {
                        request.CHIT_NO = await CodeSequenceHelper.ResolveRequiredCodeAsync(
                            _sequenceRepository,
                            session,
                            companyCd,
                            Common.NormalizeChitType(request.CHIT_TYPE) ?? request.CHIT_TYPE ?? string.Empty,
                            requestedChitNo,
                            "Chit no",
                            Common.ParseNullableDateTimeText(request.CHIT_YMD));
                    }
                    else
                    {
                        request.CHIT_NO = existingHeader?.CHIT_NO;
                    }
                }

                if (string.IsNullOrWhiteSpace(request.CHIT_CD))
                {
                    request.CHIT_CD = Common.GenerateKeyCd("C");
                }

                if (string.IsNullOrWhiteSpace(request.CHIT_NO))
                {
                    request.CHIT_NO = await CodeSequenceHelper.ResolveRequiredCodeAsync(
                        _sequenceRepository,
                        session,
                        companyCd,
                        Common.NormalizeChitType(request.CHIT_TYPE) ?? request.CHIT_TYPE ?? string.Empty,
                        null,
                        "Chit no",
                        Common.ParseNullableDateTimeText(request.CHIT_YMD));
                }

                const string upsertHeaderQuery = @"CALL setchitinfo(
                    @p_CHIT_ID,
                    @p_COMPANY_CD,
                    @p_INPUT_TYPE,
                    @p_CHIT_CD,
                    @p_CHIT_NO,
                    @p_CHIT_YMD,
                    @p_CHIT_TYPE,
                    @p_AMOUNT,
                    @p_PAYER_INFO,
                    @p_UPDATE_BY,
                    @p_IS_LOCK,
                    @p_ISEXCEL,
                    @p_EMAIL_EPAY,
                    @p_IS_CONFIRMED,
                    @p_NOTE,
                    @p_DAY_OF_PAYMENT,
                    @p_TIME_FOR_PAYMENT,
                    @p_IS_PAYMENT,
                    @p_CHIT_CD_COGS,
                    @p_DESCRIPTION_VIET,
                    @p_DESCRIPTION_ENG,
                    @p_DESCRIPTION_KOR
                )";

                var chitId = await session.QuerySingleAsync<long>(upsertHeaderQuery, new
                {
                    p_CHIT_ID = request.CHIT_ID,
                    p_COMPANY_CD = companyCd,
                    p_INPUT_TYPE = inputType,
                    p_CHIT_CD = request.CHIT_CD,
                    p_CHIT_NO = request.CHIT_NO,
                    p_CHIT_YMD = request.CHIT_YMD,
                    p_CHIT_TYPE = request.CHIT_TYPE,
                    p_AMOUNT = request.AMOUNT,
                    p_PAYER_INFO = request.PAYER_INFO,
                    p_UPDATE_BY = userId,
                    p_IS_LOCK = request.IS_LOCK,
                    p_ISEXCEL = request.ISEXCEL,
                    p_EMAIL_EPAY = request.EMAIL_EPAY,
                    p_IS_CONFIRMED = request.IS_CONFIRMED,
                    p_NOTE = request.NOTE,
                    p_DAY_OF_PAYMENT = request.DAY_OF_PAYMENT,
                    p_TIME_FOR_PAYMENT = request.TIME_FOR_PAYMENT,
                    p_IS_PAYMENT = request.IS_PAYMENT,
                    p_CHIT_CD_COGS = request.CHIT_CD_COGS,
                    p_DESCRIPTION_VIET = request.DESCRIPTION_VIET,
                    p_DESCRIPTION_ENG = request.DESCRIPTION_ENG,
                    p_DESCRIPTION_KOR = request.DESCRIPTION_KOR
                }, commandTimeout: cmdTimeoutSec);

                var existingDetailIds = request.CHIT_ID.HasValue && request.CHIT_ID.Value > 0
                    ? (await session.QueryAsync<long>(
                        "CALL getchitdetailids(@p_COMPANY_CD, @p_CHIT_ID, @p_INPUT_TYPE)",
                        new
                        {
                            p_COMPANY_CD = companyCd,
                            p_CHIT_ID = chitId,
                            p_INPUT_TYPE = inputType
                        },
                        commandTimeout: cmdTimeoutSec)).ToHashSet()
                    : new HashSet<long>();
                var currentDetailIds = new HashSet<long>();
                const string upsertDetailQuery = @"CALL setchitdetail(
                    @p_CHITDETAIL_ID,
                    @p_COMPANY_CD,
                    @p_INPUT_TYPE,
                    @p_CHIT_ID,
                    @p_CHITDETAIL_CD,
                    @p_CHIT_YMD,
                    @p_CHIT_VMD,
                    @p_DEBIT,
                    @p_CREDIT,
                    @p_AMOUNT,
                    @p_FC_AMOUNT,
                    @p_FC_TYPE,
                    @p_FC_RATE,
                    @p_FC_DATETIME,
                    @p_SORT,
                    @p_CHITDETAIL_VAT_CD,
                    @p_MG_CD,
                    @p_MG_CD_2,
                    @p_MR_CD,
                    @p_MR_CD2,
                    @p_BANK_ID,
                    @p_BANK_CD,
                    @p_BANK_OWN_CD,
                    @p_CUSTOMER_ID,
                    @p_CUSTOMER_CD,
                    @p_CUSTOMER_OWN_CD,
                    @p_DEPARTMENT_ID,
                    @p_DEPARTMENT_CD,
                    @p_DEPARTMENT_CD_2,
                    @p_HASINVENTORY,
                    @p_INVENTORY_YMD,
                    @p_ISPAY,
                    @p_ISCOLLECT,
                    @p_VAT_SERIAL_NO,
                    @p_VAT_CHIT_NO,
                    @p_VAT_CHIT_NO_2,
                    @p_VAT_AMOUNT,
                    @p_VAT_TAXABLE_AMOUNT,
                    @p_FO_VAT_AMOUNT,
                    @p_VAT_ISFREE,
                    @p_VAT_INVOICE_CD,
                    @p_VAT_INVOICE_NM,
                    @p_VAT_INFO_TYPE,
                    @p_VAT_COMPANY_ISSUE,
                    @p_VAT_COMPANY_ISSUE_ADDRESS,
                    @p_VAT_COMPANY_ISSUE_CD,
                    @p_VAT_COMPANY_TAXCD,
                    @p_VAT_PRODUCT_NM,
                    @p_VAT_ETC,
                    @p_VAT_YMD,
                    @p_VAT_YMD_2,
                    @p_VAT_INQUIRY_IN,
                    @p_VAT_INQUIRY_CODE,
                    @p_IS_NEXTVAT,
                    @p_UNDEFINE,
                    @p_DETAIL_DESCRIPTION_VIET,
                    @p_DETAIL_DESCRIPTION_ENG,
                    @p_DETAIL_DESCRIPTION_KOR,
                    @p_UPDATE_BY
                )";

                var shouldSyncInventoryInputLinks = SupportsInventoryInputLinks(request.CHIT_TYPE) && request.DETAILS != null;
                var shouldSyncInventoryOutputLinks = SupportsInventoryOutputLinks(request.CHIT_TYPE) && request.DETAILS != null;
                var inputLinkAssignments = new List<InventoryInputLinkAssignment>();
                var outputLinkAssignments = new List<InventoryOutputLinkAssignment>();

                foreach (var detail in request.DETAILS ?? new List<ChitDetailRequest>())
                {
                    var requestedDetailCd = Common.NormalizeNullableText(detail.CHITDETAIL_CD);
                    if (detail.CHITDETAIL_ID > 0 && string.IsNullOrWhiteSpace(requestedDetailCd) &&
                        existingDetailMap.TryGetValue(detail.CHITDETAIL_ID.GetValueOrDefault(), out var existingDetailCd) &&
                        !string.IsNullOrWhiteSpace(existingDetailCd))
                    {
                        requestedDetailCd = existingDetailCd;
                    }

                    detail.CHITDETAIL_CD = requestedDetailCd ?? Common.GenerateKeyCd("D");

                    var chitDetailId = await session.QuerySingleAsync<long>(upsertDetailQuery, new
                    {
                        p_CHITDETAIL_ID = detail.CHITDETAIL_ID,
                        p_COMPANY_CD = companyCd,
                        p_INPUT_TYPE = inputType,
                        p_CHIT_ID = chitId,
                        p_CHITDETAIL_CD = detail.CHITDETAIL_CD,
                        p_CHIT_YMD = detail.CHIT_YMD,
                        p_CHIT_VMD = detail.CHIT_VMD,
                        p_DEBIT = detail.DEBIT,
                        p_CREDIT = detail.CREDIT,
                        p_AMOUNT = detail.AMOUNT,
                        p_FC_AMOUNT = detail.FC_AMOUNT,
                        p_FC_TYPE = detail.FC_TYPE,
                        p_FC_RATE = detail.FC_RATE,
                        p_FC_DATETIME = detail.FC_DATETIME,
                        p_SORT = detail.SORT,
                        p_CHITDETAIL_VAT_CD = detail.CHITDETAIL_VAT_CD,
                        p_MG_CD = detail.MG_CD,
                        p_MG_CD_2 = detail.MG_CD_2,
                        p_MR_CD = detail.MR_CD,
                        p_MR_CD2 = detail.MR_CD2,
                        p_BANK_ID = detail.BANK_ID,
                        p_BANK_CD = detail.BANK_CD,
                        p_BANK_OWN_CD = detail.BANK_OWN_CD,
                        p_CUSTOMER_ID = detail.CUSTOMER_ID,
                        p_CUSTOMER_CD = detail.CUSTOMER_CD,
                        p_CUSTOMER_OWN_CD = detail.CUSTOMER_OWN_CD,
                        p_DEPARTMENT_ID = detail.DEPARTMENT_ID,
                        p_DEPARTMENT_CD = detail.DEPARTMENT_CD,
                        p_DEPARTMENT_CD_2 = detail.DEPARTMENT_CD_2,
                        p_HASINVENTORY = detail.HASINVENTORY,
                        p_INVENTORY_YMD = detail.INVENTORY_YMD,
                        p_ISPAY = detail.ISPAY,
                        p_ISCOLLECT = detail.ISCOLLECT,
                        p_VAT_SERIAL_NO = detail.VAT_SERIAL_NO,
                        p_VAT_CHIT_NO = detail.VAT_CHIT_NO,
                        p_VAT_CHIT_NO_2 = detail.VAT_CHIT_NO_2,
                        p_VAT_AMOUNT = detail.VAT_AMOUNT,
                        p_VAT_TAXABLE_AMOUNT = detail.VAT_TAXABLE_AMOUNT,
                        p_FO_VAT_AMOUNT = detail.FO_VAT_AMOUNT,
                        p_VAT_ISFREE = detail.VAT_ISFREE,
                        p_VAT_INVOICE_CD = detail.VAT_INVOICE_CD,
                        p_VAT_INVOICE_NM = detail.VAT_INVOICE_NM,
                        p_VAT_INFO_TYPE = detail.VAT_INFO_TYPE,
                        p_VAT_COMPANY_ISSUE = detail.VAT_COMPANY_ISSUE,
                        p_VAT_COMPANY_ISSUE_ADDRESS = detail.VAT_COMPANY_ISSUE_ADDRESS,
                        p_VAT_COMPANY_ISSUE_CD = detail.VAT_COMPANY_ISSUE_CD,
                        p_VAT_COMPANY_TAXCD = detail.VAT_COMPANY_TAXCD,
                        p_VAT_PRODUCT_NM = detail.VAT_PRODUCT_NM,
                        p_VAT_ETC = detail.VAT_ETC,
                        p_VAT_YMD = detail.VAT_YMD,
                        p_VAT_YMD_2 = detail.VAT_YMD_2,
                        p_VAT_INQUIRY_IN = detail.VAT_INQUIRY_IN,
                        p_VAT_INQUIRY_CODE = detail.VAT_INQUIRY_CODE,
                        p_IS_NEXTVAT = detail.IS_NEXTVAT,
                        p_UNDEFINE = detail.UNDEFINE,
                        p_DETAIL_DESCRIPTION_VIET = detail.DETAIL_DESCRIPTION_VIET,
                        p_DETAIL_DESCRIPTION_ENG = detail.DETAIL_DESCRIPTION_ENG,
                        p_DETAIL_DESCRIPTION_KOR = detail.DETAIL_DESCRIPTION_KOR,
                        p_UPDATE_BY = userId
                    }, commandTimeout: cmdTimeoutSec);

                    currentDetailIds.Add(chitDetailId);

                    if (shouldSyncInventoryInputLinks)
                    {
                        foreach (var input in detail.INVENTORY_INPUTS ?? new List<InventoryInput>())
                        {
                            if (!IsInventoryInputLinkCandidate(input))
                            {
                                continue;
                            }

                            inputLinkAssignments.Add(new InventoryInputLinkAssignment
                            {
                                INPUT_ID = input.INPUT_ID.GetValueOrDefault(),
                                CHITDETAIL_ID = chitDetailId,
                                CHITDETAIL_CD = detail.CHITDETAIL_CD ?? await GetChitDetailCodeAsync(session, companyCd, chitDetailId)
                            });
                        }
                    }

                    if (shouldSyncInventoryOutputLinks)
                    {
                        foreach (var output in detail.INVENTORY_OUTPUTS ?? new List<InventoryOutput>())
                        {
                            if (!IsInventoryOutputLinkCandidate(output))
                            {
                                continue;
                            }

                            outputLinkAssignments.Add(new InventoryOutputLinkAssignment
                            {
                                OUTPUT_ID = output.OUTPUT_ID.GetValueOrDefault(),
                                CHITDETAIL_ID = chitDetailId,
                                CHITDETAIL_CD = detail.CHITDETAIL_CD ?? await GetChitDetailCodeAsync(session, companyCd, chitDetailId)
                            });
                        }
                    }
                }

                foreach (var removedDetailId in existingDetailIds.Except(currentDetailIds))
                {
                    await session.ExecuteAsync("CALL delchitdetail(@p_COMPANY_CD, @p_INPUT_TYPE, @p_CHITDETAIL_ID, @p_UPDATE_BY)", new
                    {
                        p_COMPANY_CD = companyCd,
                        p_INPUT_TYPE = inputType,
                        p_CHITDETAIL_ID = removedDetailId,
                        p_UPDATE_BY = userId
                    }, commandTimeout: cmdTimeoutSec);
                }

                if (shouldSyncInventoryInputLinks)
                {
                    await SyncInventoryInputLinksAsync(session, companyCd, userId, chitId, inputLinkAssignments);
                }

                if (shouldSyncInventoryOutputLinks)
                {
                    await SyncInventoryOutputLinksAsync(session, companyCd, userId, chitId, outputLinkAssignments);
                }

                var newData = JsonSerializer.Serialize(new
                {
                    Header = new
                    {
                        COMPANY_CD = companyCd,
                        INPUT_TYPE = inputType,
                        request.CHIT_CD,
                        request.CHIT_NO,
                        request.CHIT_YMD,
                        request.CHIT_TYPE,
                        request.AMOUNT,
                        request.PAYER_INFO,
                        CREATE_BY = existingHeader?.CREATE_BY ?? userId,
                        UPDATE_BY = userId,
                        request.IS_LOCK,
                        request.ISEXCEL,
                        request.EMAIL_EPAY,
                        request.IS_CONFIRMED,
                        request.NOTE,
                        request.DAY_OF_PAYMENT,
                        request.TIME_FOR_PAYMENT,
                        request.IS_PAYMENT,
                        request.CHIT_CD_COGS,
                        request.DESCRIPTION_VIET,
                        request.DESCRIPTION_ENG,
                        request.DESCRIPTION_KOR,
                        CHIT_ID = chitId
                    },
                    Details = request.DETAILS
                });

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    companyCd,
                    existingHeader == null ? "INSERT" : "UPDATE",
                    "ChitInfo",
                    "chitinfo",
                    request.CHIT_CD ?? chitId.ToString(),
                    oldData,
                    newData,
                    $"Upsert chit info ({inputType})");

                session.Commit();
                return chitId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteChitInfoAsync(string companyCd, string inputType, long chitId, string userId)
        {
            var existingHeader = (await GetChitInfosAsync(companyCd, inputType, null, chitId, null)).FirstOrDefault();
            var existingDetails = existingHeader == null
                ? new List<ChitDetail>()
                : (await GetChitDetailsByChitIdsAsync(companyCd, inputType, new[] { chitId })).ToList();
            var oldData = existingHeader == null
                ? string.Empty
                : JsonSerializer.Serialize(new
                {
                    Header = existingHeader,
                    Details = existingDetails
                });

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                if (SupportsInventoryInputLinks(existingHeader?.CHIT_TYPE))
                {
                    await ClearInventoryInputLinksByLinkedChitIdAsync(session, companyCd, userId, chitId);
                }

                if (SupportsInventoryOutputLinks(existingHeader?.CHIT_TYPE))
                {
                    await ClearInventoryOutputLinksByLinkedChitIdAsync(session, companyCd, userId, chitId);
                }

                const string query = "CALL delchitinfo(@p_COMPANY_CD, @p_INPUT_TYPE, @p_CHIT_ID, @p_UPDATE_BY)";
                var result = await session.ExecuteAsync(query, new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_TYPE = inputType,
                    p_CHIT_ID = chitId,
                    p_UPDATE_BY = userId
                });

                if (result >= 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        "DELETE",
                        "ChitInfo",
                        "chitinfo",
                        existingHeader?.CHIT_CD ?? chitId.ToString(),
                        oldData,
                        string.Empty,
                        $"Delete chit info ({inputType})");
                }

                session.Commit();
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<bool> ChitCdExistsAsync(string companyCd, string inputType, string chitCd, long? excludeChitId = null)
        {
            const string query = "CALL checkChitCdExists(@p_COMPANY_CD, @p_CHIT_CD, @p_INPUT_TYPE, @p_EXCLUDE_CHIT_ID)";

            var exists = (await _db.QueryAsync<int>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_CD = chitCd,
                    p_INPUT_TYPE = inputType,
                    p_EXCLUDE_CHIT_ID = excludeChitId
                })).FirstOrDefault();

            return exists > 0;
        }

        private static bool IsValidInventoryInput(InventoryInput? input)
        {
            if (input == null || string.Equals(input.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (input.PRODUCT_ID ?? 0) > 0
                && (input.STORE_ID ?? 0) > 0
                && (input.UNIT_ID ?? 0) > 0
                && input.QUANTITY > 0;
        }

        private static bool SupportsInventoryInput(string? chitType)
        {
            return string.Equals(chitType, "IR", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidInventoryOutput(InventoryOutput? output)
        {
            if (output == null || string.Equals(output.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (output.PRODUCT_ID ?? 0) > 0
                && (output.STORE_ID ?? 0) > 0
                && (output.UNIT_ID ?? 0) > 0
                && output.QUANTITY > 0;
        }

        private static bool SupportsInventoryOutput(string? chitType)
        {
            return string.Equals(chitType, "IO", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SupportsSalesInventoryOutputLinks(string? chitType)
        {
            return SupportsInventoryOutputLinks(chitType);
        }

        private static bool SupportsInventoryInputLinks(string? chitType)
        {
            var normalized = (chitType ?? string.Empty).Trim().ToUpperInvariant();
            return normalized == "PO" || normalized == "PD" || normalized == "SR";
        }

        private static bool SupportsInventoryOutputLinks(string? chitType)
        {
            var normalized = (chitType ?? string.Empty).Trim().ToUpperInvariant();
            return normalized == "SO" || normalized == "SD" || normalized == "PR";
        }

        private static bool StoresInventoryWithoutDetails(string? chitType)
        {
            return SupportsInventoryInput(chitType) || SupportsInventoryOutput(chitType);
        }

        private static bool IsInventoryOutputLinkCandidate(InventoryOutput? output)
        {
            return output != null
                && !string.Equals(output.ISDEL, "1", StringComparison.OrdinalIgnoreCase)
                && output.OUTPUT_ID.GetValueOrDefault() > 0;
        }

        private static bool IsInventoryInputLinkCandidate(InventoryInput? input)
        {
            return input != null
                && !string.Equals(input.ISDEL, "1", StringComparison.OrdinalIgnoreCase)
                && input.INPUT_ID.GetValueOrDefault() > 0;
        }

        private static async Task<string> GetChitDetailCodeAsync(
            DapperSession session,
            string companyCd,
            long chitDetailId)
        {
            if (chitDetailId <= 0)
            {
                return string.Empty;
            }

            var codes = await session.QueryAsync<string>(
                "CALL getChitDetailCode(@p_COMPANY_CD, @p_CHITDETAIL_ID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHITDETAIL_ID = chitDetailId
                });

            return codes.FirstOrDefault() ?? string.Empty;
        }

        private static async Task<HashSet<long>> GetInventoryInputIdsByChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryInputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_IDS = chitId.ToString()
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static async Task<HashSet<long>> GetInventoryOutputIdsByChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryOutputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_IDS = chitId.ToString()
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static async Task<HashSet<long>> GetInventoryInputIdsByLinkedChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryInputIdsByLinkedChitId(@p_COMPANY_CD, @p_CHIT_ID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_ID = chitId
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static async Task<HashSet<long>> GetInventoryOutputIdsByLinkedChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryOutputIdsByLinkedChitId(@p_COMPANY_CD, @p_CHIT_ID)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_ID = chitId
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static async Task<IReadOnlyList<InventoryInputLinkRow>> GetInventoryInputLinkRowsAsync(
            DapperSession session,
            string companyCd,
            IEnumerable<long> inputIds)
        {
            var normalizedIds = inputIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return new List<InventoryInputLinkRow>();
            }

            var rows = await session.QueryAsync<InventoryInputLinkRow>(
                "CALL getChitInventoryInputLinkRows(@p_COMPANY_CD, @p_INPUT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_IDS = string.Join("|", normalizedIds)
                });

            return rows.ToList();
        }

        private static async Task<IReadOnlyList<InventoryOutputLinkRow>> GetInventoryOutputLinkRowsAsync(
            DapperSession session,
            string companyCd,
            IEnumerable<long> outputIds)
        {
            var normalizedIds = outputIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return new List<InventoryOutputLinkRow>();
            }

            var rows = await session.QueryAsync<InventoryOutputLinkRow>(
                "CALL getChitInventoryOutputLinkRows(@p_COMPANY_CD, @p_OUTPUT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_OUTPUT_IDS = string.Join("|", normalizedIds)
                });

            return rows.ToList();
        }

        private static Task SetInventoryOutputLinkAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long outputId,
            long? chitDetailId,
            string? chitDetailCd)
        {
            return session.ExecuteAsync(
                "CALL setChitInventoryOutputLink(@p_COMPANY_CD, @p_OUTPUT_ID, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_OUTPUT_ID = outputId,
                    p_CHITDETAIL_ID = chitDetailId > 0 ? chitDetailId : null,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(chitDetailCd) ? null : chitDetailCd.Trim(),
                    p_USER = userId
                });
        }

        private static Task SetInventoryInputLinkAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long inputId,
            long? chitDetailId,
            string? chitDetailCd)
        {
            return session.ExecuteAsync(
                "CALL setChitInventoryInputLink(@p_COMPANY_CD, @p_INPUT_ID, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_ID = inputId,
                    p_CHITDETAIL_ID = chitDetailId > 0 ? chitDetailId : null,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(chitDetailCd) ? null : chitDetailCd.Trim(),
                    p_USER = userId
                });
        }

        private static async Task ClearInventoryInputLinksByLinkedChitIdAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId)
        {
            var linkedInputIds = await GetInventoryInputIdsByLinkedChitIdAsync(session, companyCd, chitId);

            foreach (var inputId in linkedInputIds)
            {
                await SetInventoryInputLinkAsync(session, companyCd, userId, inputId, null, null);
            }
        }

        private static async Task ClearInventoryOutputLinksByLinkedChitIdAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId)
        {
            var linkedOutputIds = await GetInventoryOutputIdsByLinkedChitIdAsync(session, companyCd, chitId);

            foreach (var outputId in linkedOutputIds)
            {
                await SetInventoryOutputLinkAsync(session, companyCd, userId, outputId, null, null);
            }
        }

        private static async Task SyncInventoryInputLinksAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long linkedChitId,
            IEnumerable<InventoryInputLinkAssignment> desiredAssignments)
        {
            var normalizedAssignments = desiredAssignments
                .Where(item => item.INPUT_ID > 0 && item.CHITDETAIL_ID > 0)
                .ToList();

            var duplicateInputIds = normalizedAssignments
                .GroupBy(item => item.INPUT_ID)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(id => id)
                .ToList();

            if (duplicateInputIds.Count > 0)
            {
                throw new InvalidOperationException($"Chi tiết phiếu nhập đã được chọn trùng trong chứng từ: {string.Join(", ", duplicateInputIds)}");
            }

            var desiredInputIds = normalizedAssignments
                .Select(item => item.INPUT_ID)
                .ToHashSet();
            var existingLinkedInputIds = await GetInventoryInputIdsByLinkedChitIdAsync(session, companyCd, linkedChitId);
            var currentLinkRows = await GetInventoryInputLinkRowsAsync(session, companyCd, desiredInputIds);
            var existingInputIds = currentLinkRows
                .Select(item => item.INPUT_ID)
                .Where(id => id > 0)
                .ToHashSet();

            var missingInputIds = desiredInputIds
                .Except(existingInputIds)
                .OrderBy(id => id)
                .ToList();

            if (missingInputIds.Count > 0)
            {
                throw new InvalidOperationException($"Không tìm thấy dòng phiếu nhập kho để liên kết: {string.Join(", ", missingInputIds)}");
            }

            var conflictInputIds = currentLinkRows
                .Where(item => item.INPUT_ID > 0
                    && item.LINKED_CHIT_ID.GetValueOrDefault() > 0
                    && item.LINKED_CHIT_ID != linkedChitId)
                .Select(item => item.INPUT_ID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (conflictInputIds.Count > 0)
            {
                throw new InvalidOperationException($"Dòng phiếu nhập kho đã liên kết với chứng từ khác: {string.Join(", ", conflictInputIds)}");
            }

            foreach (var removedInputId in existingLinkedInputIds.Except(desiredInputIds))
            {
                await SetInventoryInputLinkAsync(session, companyCd, userId, removedInputId, null, null);
            }

            foreach (var assignment in normalizedAssignments)
            {
                await SetInventoryInputLinkAsync(
                    session,
                    companyCd,
                    userId,
                    assignment.INPUT_ID,
                    assignment.CHITDETAIL_ID,
                    assignment.CHITDETAIL_CD);
            }
        }

        private static async Task SyncInventoryOutputLinksAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long linkedChitId,
            IEnumerable<InventoryOutputLinkAssignment> desiredAssignments)
        {
            var normalizedAssignments = desiredAssignments
                .Where(item => item.OUTPUT_ID > 0 && item.CHITDETAIL_ID > 0)
                .ToList();

            var duplicateOutputIds = normalizedAssignments
                .GroupBy(item => item.OUTPUT_ID)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(id => id)
                .ToList();

            if (duplicateOutputIds.Count > 0)
            {
                throw new InvalidOperationException($"Chi tiết phiếu xuất đã được chọn trùng trong chứng từ: {string.Join(", ", duplicateOutputIds)}");
            }

            var desiredOutputIds = normalizedAssignments
                .Select(item => item.OUTPUT_ID)
                .ToHashSet();
            var existingLinkedOutputIds = await GetInventoryOutputIdsByLinkedChitIdAsync(session, companyCd, linkedChitId);
            var currentLinkRows = await GetInventoryOutputLinkRowsAsync(session, companyCd, desiredOutputIds);
            var existingOutputIds = currentLinkRows
                .Select(item => item.OUTPUT_ID)
                .Where(id => id > 0)
                .ToHashSet();

            var missingOutputIds = desiredOutputIds
                .Except(existingOutputIds)
                .OrderBy(id => id)
                .ToList();

            if (missingOutputIds.Count > 0)
            {
                throw new InvalidOperationException($"Không tìm thấy dòng phiếu xuất kho để liên kết: {string.Join(", ", missingOutputIds)}");
            }

            var conflictOutputIds = currentLinkRows
                .Where(item => item.OUTPUT_ID > 0
                    && item.LINKED_CHIT_ID.GetValueOrDefault() > 0
                    && item.LINKED_CHIT_ID != linkedChitId)
                .Select(item => item.OUTPUT_ID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (conflictOutputIds.Count > 0)
            {
                throw new InvalidOperationException($"Dòng phiếu xuất kho đã liên kết với chứng từ khác: {string.Join(", ", conflictOutputIds)}");
            }

            foreach (var removedOutputId in existingLinkedOutputIds.Except(desiredOutputIds))
            {
                await SetInventoryOutputLinkAsync(session, companyCd, userId, removedOutputId, null, null);
            }

            foreach (var assignment in normalizedAssignments)
            {
                await SetInventoryOutputLinkAsync(
                    session,
                    companyCd,
                    userId,
                    assignment.OUTPUT_ID,
                    assignment.CHITDETAIL_ID,
                    assignment.CHITDETAIL_CD);
            }
        }

        private static async Task<InventorySourceReferenceRow?> ResolveInventorySourceReferenceAsync(
            DapperSession session,
            string companyCd,
            long? sourceChitDetailId,
            long? sourceChitId)
        {
            // JOIN / resolve by ID only — CHIT_CD / CHITDETAIL_CD are denorm, not lookup keys.
            if (sourceChitDetailId.GetValueOrDefault() <= 0 && sourceChitId.GetValueOrDefault() <= 0)
            {
                return null;
            }

            const string query = "CALL getInventorySourceReference(@p_COMPANY_CD, @p_CHITDETAIL_ID, @p_CHIT_ID, @p_CHIT_CD, @p_CHITDETAIL_CD)";

            var detailReference = (await session.QueryAsync<InventorySourceReferenceRow>(query, new
            {
                p_COMPANY_CD = companyCd,
                p_CHITDETAIL_ID = sourceChitDetailId.GetValueOrDefault() > 0 ? sourceChitDetailId : null,
                p_CHIT_ID = sourceChitId.GetValueOrDefault() > 0 ? sourceChitId : null,
                p_CHIT_CD = (string?)null,
                p_CHITDETAIL_CD = (string?)null
            })).FirstOrDefault();

            return detailReference;
        }

        private static Task SoftDeleteInventoryInputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long? inputId = null,
            long? chitId = null)
        {
            return session.ExecuteAsync(
                "CALL delChitInventoryInput(@p_COMPANY_CD, @p_INPUT_ID, @p_CHIT_ID, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_ID = inputId > 0 ? inputId : null,
                    p_CHIT_ID = chitId > 0 ? chitId : null,
                    p_USER = userId
                });
        }

        private static Task SoftDeleteInventoryOutputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long? outputId = null,
            long? chitId = null)
        {
            return session.ExecuteAsync(
                "CALL delChitInventoryOutput(@p_COMPANY_CD, @p_OUTPUT_ID, @p_CHIT_ID, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_OUTPUT_ID = outputId > 0 ? outputId : null,
                    p_CHIT_ID = chitId > 0 ? chitId : null,
                    p_USER = userId
                });
        }

        private static async Task<long> UpsertInventoryInputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId,
            string? chitCd,
            string? chitType,
            int? fallbackSort,
            InventoryInput input)
        {
            var inventoryChitId = chitId > 0 ? (long?)chitId : input.CHIT_ID.GetValueOrDefault() > 0 ? input.CHIT_ID : null;
            var inventoryChitCd = Common.NormalizeNullableText(chitCd) ?? Common.NormalizeNullableText(input.CHIT_CD);
            var inventoryChitType = Common.NormalizeReferenceChitType(chitType) ?? Common.NormalizeReferenceChitType(input.CHIT_TYPE);
            var sourceDetailId = input.CHITDETAIL_ID.GetValueOrDefault() > 0 ? input.CHITDETAIL_ID : null;
            var hasExplicitSourceReference = sourceDetailId.GetValueOrDefault() > 0;
            var resolvedSource = await ResolveInventorySourceReferenceAsync(
                session,
                companyCd,
                sourceDetailId,
                inventoryChitId);

            if (resolvedSource != null && Common.NormalizeInventorySourceType(null, resolvedSource.CHIT_TYPE) == null)
            {
                resolvedSource = null;
            }

            string? sourceDetailCd = null;
            if (resolvedSource == null && hasExplicitSourceReference)
            {
                sourceDetailId = null;
            }
            else
            {
                sourceDetailId = sourceDetailId ?? (resolvedSource?.CHITDETAIL_ID > 0 ? resolvedSource.CHITDETAIL_ID : null);
                // Denorm only — never resolve by CD.
                sourceDetailCd = Common.NormalizeNullableText(resolvedSource?.CHITDETAIL_CD)
                    ?? Common.NormalizeNullableText(input.CHITDETAIL_CD);
                if (string.IsNullOrWhiteSpace(inventoryChitCd))
                {
                    inventoryChitCd = Common.NormalizeNullableText(resolvedSource?.CHIT_CD);
                }
            }

            return await session.QuerySingleAsync<long>(
                "CALL setChitInventoryInput(@p_INPUT_ID, @p_INPUT_CD, @p_CHIT_ID, @p_CHIT_CD, @p_CHIT_TYPE, @p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD, @p_STORE_ID, @p_STORE_CD, @p_UNIT_ID, @p_UNIT_CD, @p_QUANTITY, @p_UNIT_PRICE_CC, @p_FC_TYPE, @p_UNIT_PRICE_FC, @p_EXCHANGE_RATES, @p_AMOUNT_CC, @p_AMOUNT_FC, @p_SUMMARY, @p_INVENTORY_YMD, @p_STATE, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_SORT, @p_USER)",
                new
                {
                    p_INPUT_ID = input.INPUT_ID > 0 ? input.INPUT_ID : (long?)null,
                    p_INPUT_CD = input.INPUT_CD,
                    p_CHIT_ID = inventoryChitId,
                    p_CHIT_CD = string.IsNullOrWhiteSpace(inventoryChitCd) ? null : inventoryChitCd,
                    p_CHIT_TYPE = string.IsNullOrWhiteSpace(inventoryChitType) ? null : inventoryChitType,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = input.PRODUCT_ID,
                    p_PRODUCT_CD = input.PRODUCT_CD,
                    p_STORE_ID = input.STORE_ID,
                    p_STORE_CD = input.STORE_CD,
                    p_UNIT_ID = input.UNIT_ID,
                    p_UNIT_CD = input.UNIT_CD,
                    p_QUANTITY = input.QUANTITY,
                    p_UNIT_PRICE_CC = input.UNIT_PRICE_CC,
                    p_FC_TYPE = string.IsNullOrWhiteSpace(input.FC_TYPE) ? "VND" : input.FC_TYPE,
                    p_UNIT_PRICE_FC = input.UNIT_PRICE_FC,
                    p_EXCHANGE_RATES = input.EXCHANGE_RATES,
                    p_AMOUNT_CC = input.AMOUNT_CC,
                    p_AMOUNT_FC = input.AMOUNT_FC,
                    p_SUMMARY = input.SUMMARY,
                    p_INVENTORY_YMD = input.INVENTORY_YMD,
                    p_STATE = string.IsNullOrWhiteSpace(input.STATE) ? "1" : input.STATE,
                    p_CHITDETAIL_ID = sourceDetailId,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(sourceDetailCd) ? null : sourceDetailCd,
                    p_SORT = input.SORT.GetValueOrDefault() > 0 ? input.SORT : fallbackSort ?? 0,
                    p_USER = userId
                });
        }

        private static async Task<long> UpsertInventoryOutputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId,
            string? chitCd,
            string? chitType,
            int? fallbackSort,
            InventoryOutput output)
        {
            var inventoryChitId = chitId > 0 ? (long?)chitId : output.CHIT_ID.GetValueOrDefault() > 0 ? output.CHIT_ID : null;
            var inventoryChitCd = Common.NormalizeNullableText(chitCd) ?? Common.NormalizeNullableText(output.CHIT_CD);
            var inventoryChitType = Common.NormalizeReferenceChitType(chitType) ?? Common.NormalizeReferenceChitType(output.CHIT_TYPE);
            var sourceDetailId = output.CHITDETAIL_ID.GetValueOrDefault() > 0 ? output.CHITDETAIL_ID : null;
            var hasExplicitSourceReference = sourceDetailId.GetValueOrDefault() > 0;
            var resolvedSource = await ResolveInventorySourceReferenceAsync(
                session,
                companyCd,
                sourceDetailId,
                inventoryChitId);

            if (resolvedSource != null && Common.NormalizeInventorySourceType(null, resolvedSource.CHIT_TYPE) == null)
            {
                resolvedSource = null;
            }

            string? sourceDetailCd = null;
            if (resolvedSource == null && hasExplicitSourceReference)
            {
                sourceDetailId = null;
            }
            else
            {
                sourceDetailId = sourceDetailId ?? (resolvedSource?.CHITDETAIL_ID > 0 ? resolvedSource.CHITDETAIL_ID : null);
                sourceDetailCd = Common.NormalizeNullableText(resolvedSource?.CHITDETAIL_CD)
                    ?? Common.NormalizeNullableText(output.CHITDETAIL_CD);
                if (string.IsNullOrWhiteSpace(inventoryChitCd))
                {
                    inventoryChitCd = Common.NormalizeNullableText(resolvedSource?.CHIT_CD);
                }
            }

            return await session.QuerySingleAsync<long>(
                "CALL setChitInventoryOutput(@p_OUTPUT_ID, @p_OUTPUT_CD, @p_CHIT_ID, @p_CHIT_CD, @p_CHIT_TYPE, @p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD, @p_STORE_ID, @p_STORE_CD, @p_UNIT_ID, @p_UNIT_CD, @p_QUANTITY, @p_UNIT_PRICE_CC, @p_FC_TYPE, @p_UNIT_PRICE_FC, @p_EXCHANGE_RATES, @p_AMOUNT_CC, @p_AMOUNT_FC, @p_SUMMARY, @p_INVENTORY_YMD, @p_STATE, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_SORT, @p_USER)",
                new
                {
                    p_OUTPUT_ID = output.OUTPUT_ID > 0 ? output.OUTPUT_ID : (long?)null,
                    p_OUTPUT_CD = output.OUTPUT_CD,
                    p_CHIT_ID = inventoryChitId,
                    p_CHIT_CD = string.IsNullOrWhiteSpace(inventoryChitCd) ? null : inventoryChitCd,
                    p_CHIT_TYPE = string.IsNullOrWhiteSpace(inventoryChitType) ? null : inventoryChitType,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = output.PRODUCT_ID,
                    p_PRODUCT_CD = output.PRODUCT_CD,
                    p_STORE_ID = output.STORE_ID,
                    p_STORE_CD = output.STORE_CD,
                    p_UNIT_ID = output.UNIT_ID,
                    p_UNIT_CD = output.UNIT_CD,
                    p_QUANTITY = output.QUANTITY,
                    p_UNIT_PRICE_CC = output.UNIT_PRICE_CC,
                    p_FC_TYPE = string.IsNullOrWhiteSpace(output.FC_TYPE) ? "VND" : output.FC_TYPE,
                    p_UNIT_PRICE_FC = output.UNIT_PRICE_FC,
                    p_EXCHANGE_RATES = output.EXCHANGE_RATES,
                    p_AMOUNT_CC = output.AMOUNT_CC,
                    p_AMOUNT_FC = output.AMOUNT_FC,
                    p_SUMMARY = output.SUMMARY,
                    p_INVENTORY_YMD = output.INVENTORY_YMD,
                    p_STATE = string.IsNullOrWhiteSpace(output.STATE) ? "1" : output.STATE,
                    p_CHITDETAIL_ID = sourceDetailId,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(sourceDetailCd) ? null : sourceDetailCd,
                    p_SORT = output.SORT.GetValueOrDefault() > 0 ? output.SORT : fallbackSort ?? 0,
                    p_USER = userId
                });
        }
    }
}
