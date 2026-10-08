using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services;
using System.Globalization;

namespace API_AMNOTE_WEB.Repositories
{
    public class FixedAssetRepository : IFixedAssetRepository
    {
        private readonly DapperExecutor _db;

        public FixedAssetRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<FixedAssetListItemDto>> GetListAsync(string companyCd, string? status, string? accCd)
        {
            const string store = "CALL sp_fa_asset_get(@CompanyCd, @AssetId, @Status, @AccCd);";

            var normalizedStatus = NormalizeStatusFilter(status);

            var rows = await _db.QueryAsync<FixedAssetListItemDto>(
                Net_DB.Net_DB_Company,
                store,
                new
                {
                    CompanyCd = companyCd,
                    AssetId = (long?)null,
                    Status = normalizedStatus,
                    AccCd = string.IsNullOrWhiteSpace(accCd) ? null : accCd.Trim()
                });

            var list = rows.ToList();
            foreach (var item in list)
            {
                item.STATUS = NormalizeStatus(item.STATUS);
            }

            return list;
        }

        public async Task<FixedAssetDetailResponse?> GetByIdAsync(string companyCd, long assetId)
        {
            const string assetStore = "CALL sp_fa_asset_get(@CompanyCd, @AssetId, @Status, @AccCd);";
            const string allocStore = "CALL sp_fa_asset_alloc_get_by_asset(@CompanyCd, @AssetId);";

            var args = new
            {
                CompanyCd = companyCd,
                AssetId = (long?)assetId,
                Status = (string?)null,
                AccCd = (string?)null
            };

            var assetRows = await _db.QueryAsync<FixedAssetDto>(Net_DB.Net_DB_Company, assetStore, args);
            var asset = assetRows.FirstOrDefault();
            if (asset is null)
            {
                return null;
            }

            asset.STATUS = NormalizeStatus(asset.STATUS);

            var allocations = await _db.QueryAsync<FixedAssetAllocationDto>(Net_DB.Net_DB_Company, allocStore, args);

            return new FixedAssetDetailResponse
            {
                ASSET = asset,
                ALLOCATIONS = allocations.ToList()
            };
        }

        public async Task<long> CreateAsync(FixedAssetSaveRequest request, string userId)
        {
            ValidateRequest(request, isCreate: true);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);

            try
            {
                var asset = NormalizeAsset(request.ASSET);
                await EnsureAssetCodeNotDuplicatedAsync(session, asset.COMPANY_CD, asset.ASSET_CD, null);

                const string insertAssetStore = @"CALL sp_fa_asset_insert(
                    @COMPANY_CD,
                    @ASSET_CD,
                    @ASSET_NM,
                    @ACC_CD,
                    @USE_DEPT_CD,
                    @RECEIVE_YMD,
                    @USE_START_YMD,
                    @DEPRE_START_YM,
                    @DEPRE_END_YM,
                    @USEFUL_LIFE_MONTH,
                    @NORMAL_MONTH_COUNT,
                    @ORIGINAL_AMT,
                    @ACCUM_DEPRE_AMT,
                    @REMAIN_DEPRE_AMT,
                    @FIRST_DEPRE_AMT,
                    @NORMAL_DEPRE_AMT,
                    @LAST_DEPRE_AMT,
                    @ACQ_CHITINFO_ID,
                    @ACQ_CHITDETAIL_ID,
                    @ACQ_CHIT_NO,
                    @STATUS,
                    @NOTE,
                    @UserId
                );";

                var assetId = await session.QuerySingleAsync<long>(insertAssetStore, new
                {
                    asset.COMPANY_CD,
                    asset.ASSET_CD,
                    asset.ASSET_NM,
                    asset.ACC_CD,
                    asset.USE_DEPT_CD,
                    asset.RECEIVE_YMD,
                    asset.USE_START_YMD,
                    asset.DEPRE_START_YM,
                    asset.DEPRE_END_YM,
                    asset.USEFUL_LIFE_MONTH,
                    asset.NORMAL_MONTH_COUNT,
                    asset.ORIGINAL_AMT,
                    asset.ACCUM_DEPRE_AMT,
                    asset.REMAIN_DEPRE_AMT,
                    asset.FIRST_DEPRE_AMT,
                    asset.NORMAL_DEPRE_AMT,
                    asset.LAST_DEPRE_AMT,
                    asset.ACQ_CHITINFO_ID,
                    asset.ACQ_CHITDETAIL_ID,
                    asset.ACQ_CHIT_NO,
                    asset.STATUS,
                    asset.NOTE,
                    UserId = userId
                });

                await InsertAllocationsAsync(session, asset.COMPANY_CD, assetId, request.ALLOCATIONS, userId);

                session.Commit();
                return assetId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task UpdateAsync(long assetId, FixedAssetSaveRequest request, string userId)
        {
            ValidateRequest(request, isCreate: false);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);

            try
            {
                var asset = NormalizeAsset(request.ASSET);
                asset.ASSET_ID = assetId;

                await EnsureAssetExistsAsync(session, asset.COMPANY_CD, assetId);
                await EnsureAssetCodeNotDuplicatedAsync(session, asset.COMPANY_CD, asset.ASSET_CD, assetId);

                const string updateAssetStore = @"CALL sp_fa_asset_update(
                    @ASSET_ID,
                    @COMPANY_CD,
                    @ASSET_CD,
                    @ASSET_NM,
                    @ACC_CD,
                    @USE_DEPT_CD,
                    @RECEIVE_YMD,
                    @USE_START_YMD,
                    @DEPRE_START_YM,
                    @DEPRE_END_YM,
                    @USEFUL_LIFE_MONTH,
                    @NORMAL_MONTH_COUNT,
                    @ORIGINAL_AMT,
                    @ACCUM_DEPRE_AMT,
                    @REMAIN_DEPRE_AMT,
                    @FIRST_DEPRE_AMT,
                    @NORMAL_DEPRE_AMT,
                    @LAST_DEPRE_AMT,
                    @ACQ_CHITINFO_ID,
                    @ACQ_CHITDETAIL_ID,
                    @ACQ_CHIT_NO,
                    @STATUS,
                    @NOTE,
                    @UserId
                );";

                await session.ExecuteAsync(updateAssetStore, new
                {
                    asset.ASSET_ID,
                    asset.COMPANY_CD,
                    asset.ASSET_CD,
                    asset.ASSET_NM,
                    asset.ACC_CD,
                    asset.USE_DEPT_CD,
                    asset.RECEIVE_YMD,
                    asset.USE_START_YMD,
                    asset.DEPRE_START_YM,
                    asset.DEPRE_END_YM,
                    asset.USEFUL_LIFE_MONTH,
                    asset.NORMAL_MONTH_COUNT,
                    asset.ORIGINAL_AMT,
                    asset.ACCUM_DEPRE_AMT,
                    asset.REMAIN_DEPRE_AMT,
                    asset.FIRST_DEPRE_AMT,
                    asset.NORMAL_DEPRE_AMT,
                    asset.LAST_DEPRE_AMT,
                    asset.ACQ_CHITINFO_ID,
                    asset.ACQ_CHITDETAIL_ID,
                    asset.ACQ_CHIT_NO,
                    asset.STATUS,
                    asset.NOTE,
                    UserId = userId
                });

                const string softDeleteAllocStore = "CALL sp_fa_asset_alloc_soft_delete_by_asset(@CompanyCd, @AssetId, @UserId);";
                await session.ExecuteAsync(softDeleteAllocStore, new
                {
                    CompanyCd = asset.COMPANY_CD,
                    AssetId = assetId,
                    UserId = userId
                });

                await InsertAllocationsAsync(session, asset.COMPANY_CD, assetId, request.ALLOCATIONS, userId);

                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task DeleteAsync(string companyCd, long assetId, string userId)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);

            try
            {
                const string deleteStore = "CALL sp_fa_asset_delete(@CompanyCd, @AssetId, @UserId);";

                var rows = await session.QuerySingleAsync<int>(deleteStore, new
                {
                    CompanyCd = companyCd,
                    AssetId = assetId,
                    UserId = userId
                });

                if (rows == 0)
                {
                    throw new InvalidOperationException("Không tìm thấy tài sản cần xóa.");
                }

                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static FixedAssetDto NormalizeAsset(FixedAssetDto asset)
        {
            asset.COMPANY_CD = asset.COMPANY_CD.Trim();
            asset.ASSET_CD = asset.ASSET_CD.Trim();
            asset.ASSET_NM = asset.ASSET_NM.Trim();
            asset.ACC_CD = asset.ACC_CD?.Trim() ?? string.Empty;
            asset.DEPRE_START_YM = asset.DEPRE_START_YM.Trim();
            asset.DEPRE_END_YM = asset.DEPRE_END_YM.Trim();
            asset.STATUS = NormalizeStatus(asset.STATUS);
            asset.RECEIVE_YMD = NormalizeOptionalYmdCompact(asset.RECEIVE_YMD);
            asset.USE_START_YMD = NormalizeRequiredYmdCompact(asset.USE_START_YMD);
            asset.REMAIN_DEPRE_AMT = asset.ORIGINAL_AMT - asset.ACCUM_DEPRE_AMT;
            return asset;
        }

        private static string? NormalizeOptionalYmdCompact(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return NormalizeRequiredYmdCompact(value);
        }

        private static string NormalizeRequiredYmdCompact(string value)
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

            return FixedAssetDepreciationCalculator.ParseYmdCompact(trimmed).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = string.IsNullOrWhiteSpace(status) ? "IN_USE" : status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "USING" => "IN_USE",
                "STOP" => "SUSPENDED",
                "FINISHED" => "SOLD",
                _ => normalized
            };
        }

        private static string? NormalizeStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = NormalizeStatus(status);
            return AllowedAssetStatuses.Contains(normalized) ? normalized : null;
        }

        private static readonly HashSet<string> AllowedAssetStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "NOT_IN_USE",
            "IN_USE",
            "SUSPENDED",
            "SOLD"
        };

        private static void ValidateRequest(FixedAssetSaveRequest request, bool isCreate)
        {
            if (request.ASSET is null)
            {
                throw new InvalidOperationException("Thiếu thông tin tài sản.");
            }

            var asset = request.ASSET;
            asset.STATUS = NormalizeStatus(asset.STATUS);

            if (!AllowedAssetStatuses.Contains(asset.STATUS))
            {
                throw new InvalidOperationException("Trạng thái tài sản không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(asset.COMPANY_CD))
            {
                throw new InvalidOperationException("Thiếu mã công ty.");
            }

            if (string.IsNullOrWhiteSpace(asset.ASSET_CD))
            {
                throw new InvalidOperationException("Vui lòng nhập mã tài sản.");
            }

            if (string.IsNullOrWhiteSpace(asset.ASSET_NM))
            {
                throw new InvalidOperationException("Vui lòng nhập tên tài sản.");
            }

            if (string.IsNullOrWhiteSpace(asset.DEPRE_START_YM) || asset.DEPRE_START_YM.Length != 6)
            {
                throw new InvalidOperationException("Tháng bắt đầu khấu hao phải có định dạng YYYYMM.");
            }

            if (string.IsNullOrWhiteSpace(asset.DEPRE_END_YM) || asset.DEPRE_END_YM.Length != 6)
            {
                throw new InvalidOperationException("Tháng kết thúc khấu hao phải có định dạng YYYYMM.");
            }

            if (string.CompareOrdinal(asset.DEPRE_START_YM, asset.DEPRE_END_YM) > 0)
            {
                throw new InvalidOperationException("Tháng bắt đầu khấu hao không được lớn hơn tháng kết thúc khấu hao.");
            }

            if (asset.USEFUL_LIFE_MONTH <= 0)
            {
                throw new InvalidOperationException("Tổng số tháng khấu hao phải lớn hơn 0.");
            }

            if (asset.USEFUL_LIFE_MONTH > FixedAssetDepreciationCalculator.MaxUsefulLifeFullMonths)
            {
                throw new InvalidOperationException(
                    $"Tổng số tháng khấu hao không được lớn hơn {FixedAssetDepreciationCalculator.MaxUsefulLifeFullMonths}.");
            }

            if (asset.NORMAL_MONTH_COUNT < 0)
            {
                throw new InvalidOperationException("Số tháng khấu hao giữa không được nhỏ hơn 0.");
            }

            if (asset.ORIGINAL_AMT < 0)
            {
                throw new InvalidOperationException("Nguyên giá không được nhỏ hơn 0.");
            }

            if (asset.ACCUM_DEPRE_AMT < 0 || asset.ACCUM_DEPRE_AMT > asset.ORIGINAL_AMT)
            {
                throw new InvalidOperationException("Hao mòn lũy kế phải từ 0 đến nguyên giá.");
            }

            var remainDepreAmt = asset.ORIGINAL_AMT - asset.ACCUM_DEPRE_AMT;
            var planTotal = asset.FIRST_DEPRE_AMT + asset.NORMAL_DEPRE_AMT * asset.NORMAL_MONTH_COUNT + asset.LAST_DEPRE_AMT;

            if (asset.FIRST_DEPRE_AMT < 0 || asset.NORMAL_DEPRE_AMT < 0 || asset.LAST_DEPRE_AMT < 0)
            {
                throw new InvalidOperationException("Số tiền khấu hao đầu/giữa/cuối không được nhỏ hơn 0.");
            }

            if (Math.Abs(planTotal - remainDepreAmt) > 1)
            {
                throw new InvalidOperationException("Tổng khấu hao đầu + giữa * số tháng giữa + cuối phải bằng giá trị còn lại cần khấu hao.");
            }

            if (request.ALLOCATIONS is null || request.ALLOCATIONS.Count == 0)
            {
                throw new InvalidOperationException("Vui lòng nhập ít nhất một dòng phân bổ khấu hao.");
            }

            ValidateAllocations(asset, request.ALLOCATIONS);
        }

        private const decimal AllocationAmountTolerance = 1m;
        private const decimal AllocationRateTolerance = 0.0001m;

        private static string BuildAllocationMismatchMessage(string label, decimal allocated, decimal required)
        {
            var diff = Math.Round(allocated - required, 0, MidpointRounding.AwayFromZero);
            if (diff < 0)
            {
                return $"Tổng {label} đang phân bổ: {allocated:N0}. Số tiền cần phân bổ: {required:N0}. Còn thiếu: {-diff:N0}.";
            }

            return $"Tổng {label} đang phân bổ: {allocated:N0}. Số tiền cần phân bổ: {required:N0}. Đang vượt: {diff:N0}.";
        }

        private static void ValidateAllocations(FixedAssetDto asset, IReadOnlyList<FixedAssetAllocationDto> allocations)
        {
            foreach (var row in allocations)
            {
                row.ALLOC_TYPE = row.ALLOC_TYPE.Trim().ToUpperInvariant();
                row.CREDIT_ACCT_CD = string.IsNullOrWhiteSpace(row.CREDIT_ACCT_CD) ? "214" : row.CREDIT_ACCT_CD.Trim();
                row.DEBIT_ACCT_CD = row.DEBIT_ACCT_CD?.Trim() ?? string.Empty;

                if (row.ALLOC_SEQ <= 0)
                {
                    throw new InvalidOperationException("Thứ tự dòng phân bổ phải lớn hơn 0.");
                }

                if (row.ALLOC_TYPE != "PERCENT" && row.ALLOC_TYPE != "AMOUNT")
                {
                    throw new InvalidOperationException("Kiểu phân bổ chỉ nhận PERCENT hoặc AMOUNT.");
                }

                if (string.IsNullOrWhiteSpace(row.DEBIT_ACCT_CD))
                {
                    throw new InvalidOperationException("Vui lòng nhập tài khoản Nợ cho dòng phân bổ.");
                }

                if (string.IsNullOrWhiteSpace(row.CREDIT_ACCT_CD))
                {
                    throw new InvalidOperationException("Vui lòng nhập tài khoản Có cho dòng phân bổ.");
                }

                if ((row.FIRST_ALLOC_AMT ?? 0) < 0 || (row.NORMAL_ALLOC_AMT ?? 0) < 0 || (row.LAST_ALLOC_AMT ?? 0) < 0)
                {
                    throw new InvalidOperationException("Số tiền phân bổ không được nhỏ hơn 0.");
                }

                if ((row.ALLOC_RATE ?? 0) < 0)
                {
                    throw new InvalidOperationException("Tỷ lệ phân bổ không được nhỏ hơn 0.");
                }

                if ((row.ALLOC_RATE ?? 0) > 100)
                {
                    throw new InvalidOperationException("Tỷ lệ phân bổ trên một dòng không được lớn hơn 100%.");
                }
            }

            var percentRows = allocations.Where(x => x.ALLOC_TYPE.Equals("PERCENT", StringComparison.OrdinalIgnoreCase)).ToList();
            var amountRows = allocations.Where(x => x.ALLOC_TYPE.Equals("AMOUNT", StringComparison.OrdinalIgnoreCase)).ToList();

            if (percentRows.Count > 0 && amountRows.Count > 0)
            {
                throw new InvalidOperationException("Một tài sản chỉ nên dùng một kiểu phân bổ: PERCENT hoặc AMOUNT.");
            }

            if (percentRows.Count > 0)
            {
                var rateTotal = percentRows.Sum(x => x.ALLOC_RATE ?? 0);
                if (Math.Abs(rateTotal - 100) > AllocationRateTolerance)
                {
                    throw new InvalidOperationException($"Tổng tỷ lệ phân bổ phải bằng 100%. Hiện tại: {rateTotal}%.");
                }
            }

            var firstTotal = allocations.Sum(x => x.FIRST_ALLOC_AMT ?? 0);
            var normalTotal = allocations.Sum(x => x.NORMAL_ALLOC_AMT ?? 0);
            var lastTotal = allocations.Sum(x => x.LAST_ALLOC_AMT ?? 0);

            if (Math.Abs(firstTotal - asset.FIRST_DEPRE_AMT) > AllocationAmountTolerance)
            {
                throw new InvalidOperationException(BuildAllocationMismatchMessage("Tiền đầu", firstTotal, asset.FIRST_DEPRE_AMT));
            }

            if (Math.Abs(normalTotal - asset.NORMAL_DEPRE_AMT) > AllocationAmountTolerance)
            {
                throw new InvalidOperationException(BuildAllocationMismatchMessage("Tiền giữa", normalTotal, asset.NORMAL_DEPRE_AMT));
            }

            if (Math.Abs(lastTotal - asset.LAST_DEPRE_AMT) > AllocationAmountTolerance)
            {
                throw new InvalidOperationException(BuildAllocationMismatchMessage("Tiền cuối", lastTotal, asset.LAST_DEPRE_AMT));
            }
        }

        private static async Task EnsureAssetCodeNotDuplicatedAsync(
            DapperSession session,
            string companyCd,
            string assetCd,
            long? currentAssetId)
        {
            const string store = "CALL sp_fa_asset_check_code_exists(@CompanyCd, @AssetCd, @CurrentAssetId);";

            var count = await session.QuerySingleAsync<int>(store, new
            {
                CompanyCd = companyCd,
                AssetCd = assetCd,
                CurrentAssetId = currentAssetId
            });

            if (count > 0)
            {
                throw new InvalidOperationException("Mã tài sản đã tồn tại trong công ty này.");
            }
        }

        private static async Task EnsureAssetExistsAsync(
            DapperSession session,
            string companyCd,
            long assetId)
        {
            const string store = "CALL sp_fa_asset_check_exists(@CompanyCd, @AssetId);";

            var count = await session.QuerySingleAsync<int>(store, new
            {
                CompanyCd = companyCd,
                AssetId = assetId
            });

            if (count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy tài sản cần cập nhật.");
            }
        }

        private static async Task InsertAllocationsAsync(
            DapperSession session,
            string companyCd,
            long assetId,
            IReadOnlyList<FixedAssetAllocationDto> allocations,
            string userId)
        {
            const string store = @"CALL sp_fa_asset_alloc_insert(
                @COMPANY_CD,
                @ASSET_ID,
                @ALLOC_SEQ,
                @ALLOC_TYPE,
                @ALLOC_RATE,
                @FIRST_ALLOC_AMT,
                @NORMAL_ALLOC_AMT,
                @LAST_ALLOC_AMT,
                @BALANCE_YN,
                @DEBIT_ACCT_CD,
                @CREDIT_ACCT_CD,
                @DEPARTMENT_ID,
                @NOTE,
                @UserId
            );";

            var seq = 1;
            foreach (var row in allocations.OrderBy(x => x.ALLOC_SEQ))
            {
                row.COMPANY_CD = companyCd;
                row.ASSET_ID = assetId;
                row.ALLOC_SEQ = seq++;
                row.ALLOC_TYPE = row.ALLOC_TYPE.Trim().ToUpperInvariant();
                row.CREDIT_ACCT_CD = string.IsNullOrWhiteSpace(row.CREDIT_ACCT_CD) ? "214" : row.CREDIT_ACCT_CD.Trim();
                row.DEBIT_ACCT_CD = row.DEBIT_ACCT_CD.Trim();

                await session.ExecuteAsync(store, new
                {
                    row.COMPANY_CD,
                    row.ASSET_ID,
                    row.ALLOC_SEQ,
                    row.ALLOC_TYPE,
                    row.ALLOC_RATE,
                    row.FIRST_ALLOC_AMT,
                    row.NORMAL_ALLOC_AMT,
                    row.LAST_ALLOC_AMT,
                    BALANCE_YN = "N",
                    row.DEBIT_ACCT_CD,
                    row.CREDIT_ACCT_CD,
                    row.DEPARTMENT_ID,
                    row.NOTE,
                    UserId = userId
                });
            }
        }
    }
}
