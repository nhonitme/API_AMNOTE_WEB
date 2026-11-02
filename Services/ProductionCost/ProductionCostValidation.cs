using System.Globalization;
using System.Text.RegularExpressions;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Validation thuần (không DB) cho G-K 2C-01 — dùng chung Service và unit test.
/// </summary>
public static class ProductionCostValidation
{
    private static readonly Regex PeriodYmRegex = new(@"^\d{6}$", RegexOptions.Compiled);

    public static bool IsValidPeriodYm(string? periodYm)
    {
        if (string.IsNullOrWhiteSpace(periodYm) || !PeriodYmRegex.IsMatch(periodYm))
            return false;

        if (!DateTime.TryParseExact(periodYm + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;

        return true;
    }

    public static bool IsValidOptionalRunId(long? runId) =>
        !runId.HasValue || runId.Value > 0;

    public static bool IsValidOptionalRunVersion(int? runVersion) =>
        !runVersion.HasValue || runVersion.Value > 0;

    public static string? ValidateQuery(string? periodYm, long? runId, int? runVersion)
    {
        if (!IsValidPeriodYm(periodYm))
            return "PeriodYm must be yyyyMM.";
        if (!IsValidOptionalRunId(runId))
            return "RunId must be a positive number when provided.";
        if (!IsValidOptionalRunVersion(runVersion))
            return "RunVersion must be a positive number when provided.";
        return null;
    }

    public static int ResolveDetailVersion(int? requestedVersion, int runHeaderVersion) =>
        requestedVersion is > 0 ? requestedVersion.Value : runHeaderVersion;

    /// <summary>
    /// Phân biệt version chưa tồn tại vs version hợp lệ nhưng không có dòng chi tiết.
    /// Version hiện tại trên header luôn "exists"; version nhỏ hơn header là phiên trước của cùng RUN;
    /// version lớn hơn header chỉ exists khi có detail thực tế.
    /// </summary>
    public static ProductionCostRunVersionInfo ResolveRunVersionInfo(
        int? requestedVersion,
        int runHeaderVersion,
        bool hasDetailData)
    {
        var version = ResolveDetailVersion(requestedVersion, runHeaderVersion);

        if (version == runHeaderVersion)
        {
            return new ProductionCostRunVersionInfo
            {
                RunVersion = version,
                VersionExists = true,
                HasDetailData = hasDetailData,
                Message = hasDetailData ? null : "RUN_VERSION hợp lệ nhưng chưa có dòng chi tiết."
            };
        }

        if (version < runHeaderVersion)
        {
            return new ProductionCostRunVersionInfo
            {
                RunVersion = version,
                VersionExists = true,
                HasDetailData = hasDetailData,
                Message = hasDetailData
                    ? null
                    : "RUN_VERSION thuộc phiên trước của RUN nhưng không có dòng chi tiết."
            };
        }

        // version > header
        if (hasDetailData)
        {
            return new ProductionCostRunVersionInfo
            {
                RunVersion = version,
                VersionExists = true,
                HasDetailData = true
            };
        }

        return new ProductionCostRunVersionInfo
        {
            RunVersion = version,
            VersionExists = false,
            HasDetailData = false,
            Message = $"RUN_VERSION {version} không tồn tại cho RUN này (header={runHeaderVersion})."
        };
    }

    /// <summary>
    /// Đồng bộ STATUS hiển thị cho bước mirror B-I (B1/B3/B6).
    /// B-I (<c>acc_period_lock_step</c>) là nguồn khóa; G-K giữ workflow/preview/REOPENED.
    /// Không hiển thị DONE khi B-I chưa DONE.
    /// </summary>
    public static (string Status, bool Mismatch) ResolveBiBackedStatus(string? gkStatus, string? biStatus)
    {
        var biMapped = MapBiStatusToGk(biStatus);
        var gk = NormalizeStatus(gkStatus);

        if (biMapped != null)
        {
            if (IsReopened(gk))
            {
                var mismatch = !StatusesEqual(biMapped, ProductionCostStepStatus.Done)
                               && !StatusesEqual(biMapped, ProductionCostStepStatus.Reopened);
                return (ProductionCostStepStatus.Reopened, mismatch || !StatusesEqual(biMapped, gk));
            }

            if (IsDone(gk) && !IsDone(biMapped))
                return (biMapped, true);

            if (gk == null || IsNotStarted(gk))
                return (biMapped, false);

            if (StatusesEqual(gk, biMapped))
                return (biMapped, false);

            // Lệch: ưu tiên trạng thái khóa B-I cho DONE/ERROR/PROCESSING; không tin DONE G-K.
            if (IsDone(biMapped) || IsError(biMapped) || IsProcessing(biMapped))
                return (biMapped, true);

            return (gk, true);
        }

        // Không đọc được BiStatus (không có dòng) — không bịa DONE từ G-K.
        if (gk != null)
        {
            if (IsDone(gk))
                return (ProductionCostStepStatus.NotStarted, true);
            return (gk, false);
        }

        return (ProductionCostStepStatus.NotStarted, false);
    }

    public static string? MapBiStatusToGk(string? biStatus)
    {
        if (string.IsNullOrWhiteSpace(biStatus))
            return null;

        return biStatus.Trim().ToUpperInvariant() switch
        {
            "DONE" => ProductionCostStepStatus.Done,
            "PROCESSING" => ProductionCostStepStatus.Processing,
            "ERROR" => ProductionCostStepStatus.Error,
            "OPEN" => ProductionCostStepStatus.NotStarted,
            _ => biStatus.Trim().ToUpperInvariant()
        };
    }

    private static string? NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();

    private static bool StatusesEqual(string? a, string? b) =>
        string.Equals(NormalizeStatus(a), NormalizeStatus(b), StringComparison.Ordinal);

    private static bool IsDone(string? s) =>
        string.Equals(NormalizeStatus(s), ProductionCostStepStatus.Done, StringComparison.Ordinal);

    private static bool IsError(string? s) =>
        string.Equals(NormalizeStatus(s), ProductionCostStepStatus.Error, StringComparison.Ordinal);

    private static bool IsProcessing(string? s) =>
        string.Equals(NormalizeStatus(s), ProductionCostStepStatus.Processing, StringComparison.Ordinal);

    private static bool IsNotStarted(string? s) =>
        string.Equals(NormalizeStatus(s), ProductionCostStepStatus.NotStarted, StringComparison.Ordinal);

    private static bool IsReopened(string? s) =>
        string.Equals(NormalizeStatus(s), ProductionCostStepStatus.Reopened, StringComparison.Ordinal);
}
