using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services;
using System.Globalization;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    /// <summary>
    /// Excel import for fixed-asset register (<c>FixedAssetInfo</c>).
    /// One Excel row = asset header + one allocation line.
    /// Multiple rows with the same <c>ASSET_CD</c> become one asset with multiple allocations.
    /// Depreciation plan is auto-calculated from USE_START_YMD / USEFUL_LIFE_MONTH / ORIGINAL_AMT / ACCUM_DEPRE_AMT.
    /// </summary>
    public sealed class FixedAssetInfoImportHandler : IExcelImportModuleHandler
    {
        public string ModuleCd => "FixedAssetInfo";

        private readonly IFixedAssetRepository _repository;
        private readonly IExcelImportLookupRepository _lookupRepository;
        private IReadOnlyDictionary<string, long>? _accountIdsByCode;
        private IReadOnlyDictionary<string, long>? _departmentIdsByCode;

        public FixedAssetInfoImportHandler(
            IFixedAssetRepository repository,
            IExcelImportLookupRepository lookupRepository)
        {
            _repository = repository;
            _lookupRepository = lookupRepository;
        }

        public async Task ValidateRowsAsync(
            IReadOnlyList<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            List<ExcelImportResultRowDto> validateResults,
            CancellationToken cancellationToken)
        {
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(request.CompanyCd, request.DatabaseName);

            var assetCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = rows[i];
                var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(row, i);
                var assetCd = Common.NormalizeNullableText(Common.GetString(row, "ASSET_CD"));

                if (string.IsNullOrWhiteSpace(assetCd))
                {
                    validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, "ASSET_CD", lang));
                    continue;
                }

                var isFirstRowForAsset = assetCodesInFile.Add(assetCd);

                if (isFirstRowForAsset)
                {
                    if (string.IsNullOrWhiteSpace(Common.GetString(row, "ASSET_NM")))
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, "ASSET_NM", lang));
                    }

                    if (string.IsNullOrWhiteSpace(Common.GetString(row, "USE_START_YMD")))
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, "USE_START_YMD", lang));
                    }

                    var usefulLife = Common.GetDecimal(row, "USEFUL_LIFE_MONTH");
                    if (usefulLife <= 0)
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, "USEFUL_LIFE_MONTH", lang));
                    }

                    var originalAmt = Common.GetDecimal(row, "ORIGINAL_AMT");
                    if (originalAmt < 0)
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildError(
                            rowNo,
                            "Nguyên giá không được nhỏ hơn 0."));
                    }
                }

                ExcelImportReferenceValidation.SoftOptionalCode(
                    validateResults, rows, i, rowNo, "ACC_CD", _accountIdsByCode!, lang);
                ExcelImportReferenceValidation.SoftOptionalCode(
                    validateResults, rows, i, rowNo, "DEPARTMENT_CD", _departmentIdsByCode!, lang);

                var debitAcctCd = Common.NormalizeNullableText(Common.GetString(row, "DEBIT_ACCT_CD"));
                if (string.IsNullOrWhiteSpace(debitAcctCd))
                {
                    validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, "DEBIT_ACCT_CD", lang));
                }
                else
                {
                    ExcelImportReferenceValidation.AddMissingCodeError(
                        validateResults, rowNo, "DEBIT_ACCT_CD", debitAcctCd, _accountIdsByCode!, lang);
                }

                var creditAcctCd = Common.NormalizeNullableText(Common.GetString(row, "CREDIT_ACCT_CD"));
                if (!string.IsNullOrWhiteSpace(creditAcctCd))
                {
                    ExcelImportReferenceValidation.AddMissingCodeError(
                        validateResults, rowNo, "CREDIT_ACCT_CD", creditAcctCd, _accountIdsByCode!, lang);
                }
            }
        }

        public async Task SaveAsync(
            List<Dictionary<string, object>> rows,
            ExcelImportJobRequest request,
            IExcelImportJobProgressWriter progressWriter,
            CancellationToken cancellationToken)
        {
            var companyCd = request.CompanyCd;
            var userId = request.UserId ?? string.Empty;
            var lang = ExcelImportHandlerHelper.GetRequestLanguage(request);
            await EnsureLookupMapsAsync(companyCd, request.DatabaseName);

            var groups = GroupRowsByAssetCd(rows);
            if (groups.Count == 0)
            {
                throw new InvalidOperationException(
                    await ExcelImportHandlerHelper.GetLocalizedMessageAsync("EXCEL_IMPORT_NO_DATA_IN_FILE", lang));
            }

            await progressWriter.ReportPercentAsync(
                35,
                await ExcelImportHandlerHelper.GetStartInsertTextAsync(lang),
                cancellationToken);

            var savedCount = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = groups[i];
                var saveRequest = BuildSaveRequest(companyCd, group);
                await _repository.CreateAsync(saveRequest, userId);
                savedCount++;

                var percent = 35 + (int)Math.Round((i + 1) * 65m / groups.Count);
                await progressWriter.ReportAsync(
                    i + 1,
                    groups.Count,
                    percent,
                    await ExcelImportHandlerHelper.GetProgressTransferTextAsync(i + 1, groups.Count, lang),
                    cancellationToken);
            }

            await progressWriter.ReportPercentAsync(
                100,
                await ExcelImportHandlerHelper.GetCompleteTextAsync(savedCount, groups.Count, lang),
                cancellationToken);
        }

        private static List<List<Dictionary<string, object>>> GroupRowsByAssetCd(
            IReadOnlyList<Dictionary<string, object>> rows)
        {
            var groups = new List<List<Dictionary<string, object>>>();
            var indexByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var assetCd = Common.NormalizeRequiredText(Common.GetString(row, "ASSET_CD"));
                if (indexByCode.TryGetValue(assetCd, out var existingIndex))
                {
                    groups[existingIndex].Add(row);
                    continue;
                }

                indexByCode[assetCd] = groups.Count;
                groups.Add(new List<Dictionary<string, object>> { row });
            }

            return groups;
        }

        private FixedAssetSaveRequest BuildSaveRequest(
            string companyCd,
            IReadOnlyList<Dictionary<string, object>> groupRows)
        {
            var header = groupRows[0];
            var useStartYmd = NormalizeYmdInput(Common.GetString(header, "USE_START_YMD"));
            var usefulLifeMonth = Common.GetDecimal(header, "USEFUL_LIFE_MONTH");
            var originalAmt = Common.GetDecimal(header, "ORIGINAL_AMT");
            var accumDepreAmt = Common.GetDecimal(header, "ACCUM_DEPRE_AMT");

            var calc = FixedAssetDepreciationCalculator.Calculate(
                useStartYmd,
                usefulLifeMonth,
                originalAmt,
                accumDepreAmt);

            var asset = new FixedAssetDto
            {
                ASSET_ID = 0,
                COMPANY_CD = companyCd,
                ASSET_CD = Common.NormalizeRequiredText(Common.GetString(header, "ASSET_CD")),
                ASSET_NM = Common.NormalizeRequiredText(Common.GetString(header, "ASSET_NM")),
                ACC_CD = Common.NormalizeNullableText(Common.GetString(header, "ACC_CD")) ?? string.Empty,
                USE_DEPT_CD = Common.NormalizeNullableText(Common.GetString(header, "USE_DEPT_CD")),
                RECEIVE_YMD = NormalizeOptionalYmd(Common.GetString(header, "RECEIVE_YMD")),
                USE_START_YMD = useStartYmd,
                DEPRE_START_YM = calc.DepreStartYm,
                DEPRE_END_YM = calc.DepreEndYm,
                USEFUL_LIFE_MONTH = calc.UsefulLifeMonth,
                NORMAL_MONTH_COUNT = calc.NormalMonthCount,
                ORIGINAL_AMT = originalAmt,
                ACCUM_DEPRE_AMT = accumDepreAmt,
                REMAIN_DEPRE_AMT = calc.RemainDepreAmt,
                FIRST_DEPRE_AMT = calc.FirstDepreAmt,
                NORMAL_DEPRE_AMT = calc.NormalDepreAmt,
                LAST_DEPRE_AMT = calc.LastDepreAmt,
                ACQ_CHIT_NO = Common.NormalizeNullableText(Common.GetString(header, "ACQ_CHIT_NO")),
                STATUS = NormalizeStatus(Common.GetString(header, "STATUS")),
                NOTE = Common.NormalizeNullableText(Common.GetString(header, "NOTE")),
            };

            var allocations = BuildAllocations(companyCd, asset, groupRows);
            return new FixedAssetSaveRequest
            {
                ASSET = asset,
                ALLOCATIONS = allocations,
            };
        }

        private List<FixedAssetAllocationDto> BuildAllocations(
            string companyCd,
            FixedAssetDto asset,
            IReadOnlyList<Dictionary<string, object>> groupRows)
        {
            var drafts = new List<(Dictionary<string, object> Row, string AllocType, decimal Rate, string Debit, string Credit, long? DeptId, string? Note)>();

            for (var i = 0; i < groupRows.Count; i++)
            {
                var row = groupRows[i];
                var allocType = NormalizeAllocType(Common.GetString(row, "ALLOC_TYPE"));
                var rate = Common.GetDecimal(row, "ALLOC_RATE");
                if (rate <= 0 && groupRows.Count == 1)
                {
                    rate = 100m;
                }

                var debit = Common.NormalizeRequiredText(Common.GetString(row, "DEBIT_ACCT_CD"));
                var credit = Common.NormalizeNullableText(Common.GetString(row, "CREDIT_ACCT_CD")) ?? "214";
                var deptCd = Common.NormalizeNullableText(Common.GetString(row, "DEPARTMENT_CD"));
                long? deptId = null;
                if (!string.IsNullOrWhiteSpace(deptCd)
                    && _departmentIdsByCode != null
                    && _departmentIdsByCode.TryGetValue(deptCd, out var resolvedDeptId))
                {
                    deptId = resolvedDeptId;
                }

                drafts.Add((
                    row,
                    allocType,
                    rate,
                    debit,
                    credit,
                    deptId,
                    Common.NormalizeNullableText(Common.GetString(row, "ALLOC_NOTE"))));
            }

            var isPercent = drafts.All(x => x.AllocType == "PERCENT");
            if (!isPercent)
            {
                throw new InvalidOperationException(
                    "Import tài sản cố định hiện hỗ trợ kiểu phân bổ PERCENT (tỷ lệ %).");
            }

            var rateTotal = drafts.Sum(x => x.Rate);
            if (Math.Abs(rateTotal - 100m) > 0.0001m)
            {
                throw new InvalidOperationException(
                    $"Tổng tỷ lệ phân bổ của mã {asset.ASSET_CD} phải bằng 100%. Hiện tại: {rateTotal}%.");
            }

            var allocations = new List<FixedAssetAllocationDto>();
            decimal firstAssigned = 0;
            decimal normalAssigned = 0;
            decimal lastAssigned = 0;

            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                var isLast = i == drafts.Count - 1;

                decimal firstAmt;
                decimal normalAmt;
                decimal lastAmt;

                if (isLast)
                {
                    firstAmt = asset.FIRST_DEPRE_AMT - firstAssigned;
                    normalAmt = asset.NORMAL_DEPRE_AMT - normalAssigned;
                    lastAmt = asset.LAST_DEPRE_AMT - lastAssigned;
                }
                else
                {
                    firstAmt = RoundMoney(asset.FIRST_DEPRE_AMT * draft.Rate / 100m);
                    normalAmt = RoundMoney(asset.NORMAL_DEPRE_AMT * draft.Rate / 100m);
                    lastAmt = RoundMoney(asset.LAST_DEPRE_AMT * draft.Rate / 100m);
                    firstAssigned += firstAmt;
                    normalAssigned += normalAmt;
                    lastAssigned += lastAmt;
                }

                allocations.Add(new FixedAssetAllocationDto
                {
                    ALLOC_ID = null,
                    COMPANY_CD = companyCd,
                    ASSET_ID = 0,
                    ALLOC_SEQ = i + 1,
                    ALLOC_TYPE = "PERCENT",
                    ALLOC_RATE = draft.Rate,
                    FIRST_ALLOC_AMT = firstAmt,
                    NORMAL_ALLOC_AMT = normalAmt,
                    LAST_ALLOC_AMT = lastAmt,
                    DEBIT_ACCT_CD = draft.Debit,
                    CREDIT_ACCT_CD = draft.Credit,
                    DEPARTMENT_ID = draft.DeptId,
                    NOTE = draft.Note,
                });
            }

            return allocations;
        }

        private static decimal RoundMoney(decimal value)
            => Math.Round(value, 0, MidpointRounding.AwayFromZero);

        private static string NormalizeAllocType(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "PERCENT"
                : value.Trim().ToUpperInvariant();

            return normalized switch
            {
                "AMOUNT" or "SỐ TIỀN" or "TIEN" => "AMOUNT",
                _ => "PERCENT",
            };
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status)
                ? "IN_USE"
                : status.Trim().ToUpperInvariant();

            return normalized switch
            {
                "USING" or "ĐANG SỬ DỤNG" or "DANG SU DUNG" => "IN_USE",
                "STOP" or "TẠM NGƯNG" or "TAM NGUNG" => "SUSPENDED",
                "FINISHED" or "ĐÃ BÁN" or "DA BAN" => "SOLD",
                "NOT_IN_USE" or "CHƯA SỬ DỤNG" or "CHUA SU DUNG" => "NOT_IN_USE",
                "IN_USE" or "SUSPENDED" or "SOLD" => normalized,
                _ => "IN_USE",
            };
        }

        private static string NormalizeYmdInput(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Vui lòng nhập ngày bắt đầu sử dụng.");
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 8 && trimmed.All(char.IsDigit))
            {
                _ = FixedAssetDepreciationCalculator.ParseYmdCompact(trimmed);
                return trimmed;
            }

            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)
                || DateTime.TryParseExact(
                    trimmed,
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed))
            {
                return parsed.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            return FixedAssetDepreciationCalculator.ParseYmdCompact(trimmed).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string? NormalizeOptionalYmd(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return NormalizeYmdInput(value);
        }

        private async Task EnsureLookupMapsAsync(string companyCd, string? databaseName)
        {
            _accountIdsByCode ??= await _lookupRepository.GetAccountIdsByCodeAsync(companyCd, databaseName);
            _departmentIdsByCode ??= await _lookupRepository.GetDepartmentIdsByCodeAsync(companyCd, databaseName);
        }
    }
}
