using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.Chit
{
    public sealed class ChitInfoWriteService : IChitInfoWriteService
    {
        private readonly IChitInfoRepository _repository;

        public ChitInfoWriteService(IChitInfoRepository repository)
        {
            _repository = repository;
        }

        public Task<long> CreateAsync(
            string companyCd,
            string inputType,
            string supportedType,
            string userId,
            ChitInfoRequest request,
            IReadOnlyCollection<string>? supportedTypeAliases = null,
            ChitInfoCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null)
        {
            var normalized = BuildCreateRequest(request, companyCd, supportedType, supportedTypeAliases, codeMaps, lang);
            return _repository.SaveChitInfoAsync(companyCd, inputType, userId, normalized, databaseName);
        }

        public Task<long> UpdateAsync(
            string companyCd,
            string inputType,
            string supportedType,
            string userId,
            ChitInfo existing,
            ChitInfoRequest request,
            IReadOnlyCollection<string>? supportedTypeAliases = null,
            ChitInfoCodeMaps? codeMaps = null,
            string? databaseName = null,
            string? lang = null)
        {
            var normalized = BuildUpdateRequest(existing, request, companyCd, supportedType, supportedTypeAliases, codeMaps, lang);
            return _repository.SaveChitInfoAsync(companyCd, inputType, userId, normalized, databaseName);
        }

        private static ChitInfoRequest BuildCreateRequest(
            ChitInfoRequest request,
            string companyCd,
            string supportedType,
            IReadOnlyCollection<string>? supportedTypeAliases,
            ChitInfoCodeMaps? codeMaps,
            string? lang)
        {
            if (request.CHIT_TYPE != null && NormalizeChitType(request.CHIT_TYPE, supportedType, supportedTypeAliases) == null)
            {
                throw new ArgumentException("CHIT_TYPE is invalid");
            }

            var headerDate = NormalizeNullableYmdText(request.CHIT_YMD, nameof(ChitInfoRequest.CHIT_YMD));
            var details = NormalizeDetails(request.DETAILS, companyCd, headerDate, codeMaps);

            if (details.Count == 0)
            {
                throw new ArgumentException(lang == null
                    ? "DETAILS is required"
                    : ExcelImportHandlerHelper.GetDetailsRequiredMessage(lang));
            }

            return new ChitInfoRequest
            {
                CHIT_ID = 0,
                COMPANY_CD = companyCd,
                CHIT_CD = null,
                CHIT_NO = NormalizeNullableText(request.CHIT_NO),
                CHIT_YMD = headerDate,
                CHIT_TYPE = supportedType,
                AMOUNT = NormalizeAmount(request.AMOUNT, details),
                PAYER_INFO = NormalizeNullableText(request.PAYER_INFO),
                ISDEL = "0",
                IS_LOCK = NormalizeFlagString(request.IS_LOCK, "0"),
                ISEXCEL = NormalizeFlagString(request.ISEXCEL, "0"),
                EMAIL_EPAY = NormalizeNullableText(request.EMAIL_EPAY),
                IS_CONFIRMED = NormalizeFlagString(request.IS_CONFIRMED, "0"),
                NOTE = NormalizeNullableText(request.NOTE),
                DAY_OF_PAYMENT = NormalizeNullablePositiveInt(request.DAY_OF_PAYMENT),
                TIME_FOR_PAYMENT = NormalizeNullableText(request.TIME_FOR_PAYMENT),
                IS_PAYMENT = NormalizeFlagString(request.IS_PAYMENT, "0"),
                CHIT_CD_COGS = NormalizeNullableText(request.CHIT_CD_COGS),
                DESCRIPTION_VIET = NormalizeNullableText(request.DESCRIPTION_VIET),
                DESCRIPTION_ENG = NormalizeNullableText(request.DESCRIPTION_ENG),
                DESCRIPTION_KOR = NormalizeNullableText(request.DESCRIPTION_KOR),
                DETAILS = details
            };
        }

        private static ChitInfoRequest BuildUpdateRequest(
            ChitInfo existing,
            ChitInfoRequest request,
            string companyCd,
            string supportedType,
            IReadOnlyCollection<string>? supportedTypeAliases,
            ChitInfoCodeMaps? codeMaps,
            string? lang)
        {
            if (request.CHIT_TYPE != null && NormalizeChitType(request.CHIT_TYPE, supportedType, supportedTypeAliases) == null)
            {
                throw new ArgumentException("CHIT_TYPE is invalid");
            }

            var headerDate = request.CHIT_YMD == null
                ? existing.CHIT_YMD
                : NormalizeNullableYmdText(request.CHIT_YMD, nameof(ChitInfoRequest.CHIT_YMD));
            var details = NormalizeDetails(request.DETAILS, companyCd, headerDate, codeMaps);

            if (details.Count == 0 && !AllowsEmptyInventoryLinkUpdate(supportedType))
            {
                throw new ArgumentException(lang == null
                    ? "DETAILS is required"
                    : ExcelImportHandlerHelper.GetDetailsRequiredMessage(lang));
            }

            return new ChitInfoRequest
            {
                CHIT_ID = existing.CHIT_ID,
                COMPANY_CD = companyCd,
                CHIT_CD = existing.CHIT_CD,
                CHIT_NO = request.CHIT_NO == null ? NormalizeNullableText(existing.CHIT_NO) : NormalizeNullableText(request.CHIT_NO),
                CHIT_YMD = headerDate,
                CHIT_TYPE = supportedType,
                AMOUNT = request.AMOUNT ?? NormalizeAmount(existing.AMOUNT, details),
                PAYER_INFO = request.PAYER_INFO == null ? NormalizeNullableText(existing.PAYER_INFO) : NormalizeNullableText(request.PAYER_INFO),
                ISDEL = "0",
                IS_LOCK = request.IS_LOCK == null ? NormalizeFlagString(existing.IS_LOCK, "0") : NormalizeFlagString(request.IS_LOCK, "0"),
                ISEXCEL = request.ISEXCEL == null ? NormalizeFlagString(existing.ISEXCEL, "0") : NormalizeFlagString(request.ISEXCEL, "0"),
                EMAIL_EPAY = request.EMAIL_EPAY == null ? NormalizeNullableText(existing.EMAIL_EPAY) : NormalizeNullableText(request.EMAIL_EPAY),
                IS_CONFIRMED = request.IS_CONFIRMED == null ? NormalizeFlagString(existing.IS_CONFIRMED, "0") : NormalizeFlagString(request.IS_CONFIRMED, "0"),
                NOTE = request.NOTE == null ? NormalizeNullableText(existing.NOTE) : NormalizeNullableText(request.NOTE),
                DAY_OF_PAYMENT = request.DAY_OF_PAYMENT.HasValue ? NormalizeNullablePositiveInt(request.DAY_OF_PAYMENT) : NormalizeNullablePositiveInt(existing.DAY_OF_PAYMENT),
                TIME_FOR_PAYMENT = request.TIME_FOR_PAYMENT == null ? NormalizeNullableText(existing.TIME_FOR_PAYMENT) : NormalizeNullableText(request.TIME_FOR_PAYMENT),
                IS_PAYMENT = request.IS_PAYMENT == null ? NormalizeFlagString(existing.IS_PAYMENT, "0") : NormalizeFlagString(request.IS_PAYMENT, "0"),
                CHIT_CD_COGS = request.CHIT_CD_COGS == null ? NormalizeNullableText(existing.CHIT_CD_COGS) : NormalizeNullableText(request.CHIT_CD_COGS),
                DESCRIPTION_VIET = request.DESCRIPTION_VIET == null ? NormalizeNullableText(existing.DESCRIPTION_VIET) : NormalizeNullableText(request.DESCRIPTION_VIET),
                DESCRIPTION_ENG = request.DESCRIPTION_ENG == null ? NormalizeNullableText(existing.DESCRIPTION_ENG) : NormalizeNullableText(request.DESCRIPTION_ENG),
                DESCRIPTION_KOR = request.DESCRIPTION_KOR == null ? NormalizeNullableText(existing.DESCRIPTION_KOR) : NormalizeNullableText(request.DESCRIPTION_KOR),
                DETAILS = details
            };
        }

        private static List<ChitDetailRequest> NormalizeDetails(
            IEnumerable<ChitDetailRequest>? details,
            string companyCd,
            string? headerDate,
            ChitInfoCodeMaps? codeMaps)
        {
            return (details ?? Enumerable.Empty<ChitDetailRequest>())
                .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                .Select((detail, index) =>
                {
                    var detailChitYmd = NormalizeNullableYmdText(detail.CHIT_YMD, nameof(ChitDetailRequest.CHIT_YMD)) ?? headerDate;
                    var detailChitVmd = NormalizeNullableYmdText(detail.CHIT_VMD, nameof(ChitDetailRequest.CHIT_VMD));
                    var detailInventoryYmd = NormalizeNullableYmdText(detail.INVENTORY_YMD, nameof(ChitDetailRequest.INVENTORY_YMD));
                    return new ChitDetailRequest
                    {
                        CHITDETAIL_ID = detail.CHITDETAIL_ID.HasValue && detail.CHITDETAIL_ID.Value > 0 ? detail.CHITDETAIL_ID.Value : 0,
                        COMPANY_CD = companyCd,
                        CHIT_ID = detail.CHIT_ID,
                        CHITDETAIL_CD = NormalizeNullableText(detail.CHITDETAIL_CD),
                        CHIT_YMD = detailChitYmd,
                        CHIT_VMD = detailChitVmd,
                        DEBIT = NormalizeNullableText(detail.DEBIT),
                        DEBIT_NM_VIET = NormalizeNullableText(detail.DEBIT_NM_VIET),
                        DEBIT_NM_ENG = NormalizeNullableText(detail.DEBIT_NM_ENG),
                        DEBIT_NM_KOR = NormalizeNullableText(detail.DEBIT_NM_KOR),
                        DEBIT_NM_CHINA = NormalizeNullableText(detail.DEBIT_NM_CHINA),
                        CREDIT = NormalizeNullableText(detail.CREDIT),
                        CREDIT_NM_VIET = NormalizeNullableText(detail.CREDIT_NM_VIET),
                        CREDIT_NM_ENG = NormalizeNullableText(detail.CREDIT_NM_ENG),
                        CREDIT_NM_KOR = NormalizeNullableText(detail.CREDIT_NM_KOR),
                        CREDIT_NM_CHINA = NormalizeNullableText(detail.CREDIT_NM_CHINA),
                        AMOUNT = detail.AMOUNT ?? 0,
                        FC_AMOUNT = detail.FC_AMOUNT ?? 0,
                        FC_TYPE = NormalizeNullableText(detail.FC_TYPE),
                        FC_RATE = detail.FC_RATE ?? 0,
                        FC_DATETIME = detail.FC_DATETIME,
                        SORT = detail.SORT ?? index + 1,
                        ISDEL = "0",
                        CHITDETAIL_VAT_CD = NormalizeNullableText(detail.CHITDETAIL_VAT_CD),
                        MG_CD = NormalizeNullableText(detail.MG_CD),
                        MG_CD_2 = NormalizeNullableText(detail.MG_CD_2),
                        MR_CD = NormalizeNullableText(detail.MR_CD),
                        MR_CD2 = NormalizeNullableText(detail.MR_CD2),
                        BANK_ID = ResolveCodeId(detail.BANK_CD, codeMaps?.BankIdsByCode) ?? Common.NormalizeNullablePositiveLong(detail.BANK_ID),
                        BANK_CD = NormalizeNullableText(detail.BANK_CD),
                        BANK_OWN_CD = NormalizeNullableText(detail.BANK_OWN_CD),
                        CUSTOMER_ID = ResolveCodeId(detail.CUSTOMER_CD, codeMaps?.CustomerIdsByCode) ?? detail.CUSTOMER_ID,
                        CUSTOMER_CD = NormalizeNullableText(detail.CUSTOMER_CD),
                        CUSTOMER_NM_VIET = NormalizeNullableText(detail.CUSTOMER_NM_VIET),
                        CUSTOMER_NM_ENG = NormalizeNullableText(detail.CUSTOMER_NM_ENG),
                        CUSTOMER_NM_KOR = NormalizeNullableText(detail.CUSTOMER_NM_KOR),
                        CUSTOMER_NM_CHINA = NormalizeNullableText(detail.CUSTOMER_NM_CHINA),
                        CUSTOMER_OWN_CD = NormalizeNullableText(detail.CUSTOMER_OWN_CD),
                        DEPARTMENT_ID = ResolveCodeId(detail.DEPARTMENT_CD, codeMaps?.DepartmentIdsByCode) ?? Common.NormalizeNullablePositiveLong(detail.DEPARTMENT_ID),
                        DEPARTMENT_CD = NormalizeNullableText(detail.DEPARTMENT_CD),
                        DEPARTMENT_CD_2 = NormalizeNullableText(detail.DEPARTMENT_CD_2),
                        HASINVENTORY = NormalizeFlagString(detail.HASINVENTORY, "0"),
                        INVENTORY_YMD = detailInventoryYmd,
                        ISPAY = NormalizeFlagString(detail.ISPAY, "0"),
                        ISCOLLECT = NormalizeFlagString(detail.ISCOLLECT, "0"),
                        VAT_SERIAL_NO = NormalizeNullableText(detail.VAT_SERIAL_NO),
                        VAT_CHIT_NO = NormalizeNullableText(detail.VAT_CHIT_NO),
                        VAT_CHIT_NO_2 = NormalizeNullableText(detail.VAT_CHIT_NO_2),
                        VAT_AMOUNT = detail.VAT_AMOUNT ?? 0,
                        VAT_TAXABLE_AMOUNT = detail.VAT_TAXABLE_AMOUNT ?? 0,
                        FO_VAT_AMOUNT = detail.FO_VAT_AMOUNT ?? 0,
                        VAT_ISFREE = NormalizeFlagString(detail.VAT_ISFREE, "0"),
                        VAT_INVOICE_CD = NormalizeNullableText(detail.VAT_INVOICE_CD),
                        VAT_INVOICE_NM = NormalizeNullableText(detail.VAT_INVOICE_NM),
                        VAT_INFO_TYPE = NormalizeNullableText(detail.VAT_INFO_TYPE),
                        VAT_COMPANY_ISSUE = NormalizeNullableText(detail.VAT_COMPANY_ISSUE),
                        VAT_COMPANY_ISSUE_ADDRESS = NormalizeNullableText(detail.VAT_COMPANY_ISSUE_ADDRESS),
                        VAT_COMPANY_ISSUE_CD = NormalizeNullableText(detail.VAT_COMPANY_ISSUE_CD),
                        VAT_COMPANY_TAXCD = NormalizeNullableText(detail.VAT_COMPANY_TAXCD),
                        VAT_PRODUCT_NM = NormalizeNullableText(detail.VAT_PRODUCT_NM),
                        VAT_ETC = NormalizeNullableText(detail.VAT_ETC),
                        VAT_YMD = detail.VAT_YMD,
                        VAT_YMD_2 = detail.VAT_YMD_2,
                        VAT_INQUIRY_IN = NormalizeNullableText(detail.VAT_INQUIRY_IN),
                        VAT_INQUIRY_CODE = NormalizeNullableText(detail.VAT_INQUIRY_CODE),
                        IS_NEXTVAT = NormalizeFlagString(detail.IS_NEXTVAT, "0"),
                        UNDEFINE = NormalizeNullableText(detail.UNDEFINE),
                        DETAIL_DESCRIPTION_VIET = NormalizeNullableText(detail.DETAIL_DESCRIPTION_VIET),
                        DETAIL_DESCRIPTION_ENG = NormalizeNullableText(detail.DETAIL_DESCRIPTION_ENG),
                        DETAIL_DESCRIPTION_KOR = NormalizeNullableText(detail.DETAIL_DESCRIPTION_KOR),
                        INVENTORY_INPUTS = detail.INVENTORY_INPUTS?
                            .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                            .ToList(),
                        INVENTORY_OUTPUTS = detail.INVENTORY_OUTPUTS?
                            .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                            .ToList(),
                    };
                })
                .ToList();
        }

        private static decimal NormalizeAmount(decimal? amount, IEnumerable<ChitDetailRequest> details)
        {
            return amount ?? details.Sum(item => item.AMOUNT ?? 0);
        }

        private static bool AllowsEmptyInventoryLinkUpdate(string? chitType)
        {
            var normalized = NormalizeUpperText(chitType);
            return normalized == "PO"
                || normalized == "PD"
                || normalized == "PR"
                || normalized == "SO"
                || normalized == "SD"
                || normalized == "SR";
        }

        private static string? NormalizeChitType(
            string? value,
            string supportedType,
            IReadOnlyCollection<string>? supportedTypeAliases)
        {
            var normalized = NormalizeNullableText(value)?.ToUpperInvariant();
            if (normalized == null)
            {
                return null;
            }

            if (normalized == supportedType)
            {
                return supportedType;
            }

            if (supportedTypeAliases != null && supportedTypeAliases.Contains(normalized))
            {
                return supportedType;
            }

            return null;
        }

        private static long? ResolveCodeId(string? code, IReadOnlyDictionary<string, long>? idsByCode)
        {
            if (idsByCode == null || idsByCode.Count == 0)
            {
                return null;
            }

            return ExcelImportReferenceValidation.ResolveId(code, idsByCode);
        }
    }
}
