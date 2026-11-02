namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Trạng thái RUN G-K — phân biệt xác nhận phân bổ (Bước 2) và xác nhận giá thành toàn kỳ.
/// </summary>
public static class GkRunStatus
{
    public const string Draft = "DRAFT";
    public const string Previewed = "PREVIEWED";
    /// <summary>Bước 2 đã CONFIRM phân bổ 622/627 — chưa phải giá thành toàn kỳ.</summary>
    public const string AllocConfirmed = "ALLOC_CONFIRMED";
    /// <summary>Xác nhận giá thành toàn kỳ (sau các bước tính / ghi nhận đủ).</summary>
    public const string Confirmed = "CONFIRMED";
    public const string Reopened = "REOPENED";
    public const string Superseded = "SUPERSEDED";

    public static bool IsAllocationLocked(string? runStatus) =>
        string.Equals(runStatus, AllocConfirmed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, Confirmed, StringComparison.OrdinalIgnoreCase);

    /// <summary>Bước 3 EXP_TO_154_ONLY chấp nhận RUN đã khóa phân bổ (không yêu cầu CONFIRMED toàn kỳ).</summary>
    public static bool IsReadyForExpTo154(string? runStatus) =>
        string.Equals(runStatus, AllocConfirmed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, Confirmed, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cho phép tính lại pool chỉ khi RUN ∈ DRAFT|PREVIEWED|REOPENED (hoặc trống).
    /// ALLOC_CONFIRMED/CONFIRMED chặn tuyệt đối — không phụ thuộc STEP (thiếu/chưa DONE).
    /// </summary>
    public static bool AllowsPoolRecalc(string? runStatus, string? allocationStepStatus = null)
    {
        _ = allocationStepStatus; // giữ chữ ký tương thích; không nới chặn theo STEP

        if (IsAllocationLocked(runStatus))
            return false;

        if (string.Equals(runStatus, Reopened, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(runStatus, Draft, StringComparison.OrdinalIgnoreCase)
               || string.Equals(runStatus, Previewed, StringComparison.OrdinalIgnoreCase)
               || string.IsNullOrWhiteSpace(runStatus);
    }

    /// <summary>Sau Confirm Bước 2: RUN → ALLOC_CONFIRMED (không nhảy thẳng CONFIRMED).</summary>
    public static string AfterAllocationConfirm(string? currentStatus)
    {
        if (string.Equals(currentStatus, Confirmed, StringComparison.OrdinalIgnoreCase))
            return Confirmed; // không hạ cấp
        return AllocConfirmed;
    }

    /// <summary>
    /// Sau Confirm Bước 4 (COST_CALC=DONE): không tự chuyển RUN → CONFIRMED toàn kỳ
    /// (cần B5/B6 đủ điều kiện). Giữ STATUS hiện tại; không hạ CONFIRMED.
    /// </summary>
    public static string AfterCostCalcConfirm(string? currentStatus)
    {
        if (string.Equals(currentStatus, Confirmed, StringComparison.OrdinalIgnoreCase))
            return Confirmed;
        if (string.IsNullOrWhiteSpace(currentStatus))
            return AllocConfirmed;
        return currentStatus.Trim().ToUpperInvariant();
    }

    /// <summary>COST_CALC=DONE chỉ khóa bước 4 — chưa đủ để coi là giá thành toàn kỳ.</summary>
    public static bool IsFullPeriodConfirmed(string? runStatus) =>
        string.Equals(runStatus, Confirmed, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Khóa dòng alloc_detail đồng bộ DDL UNIQUE mới.</summary>
public static class AllocDetailKey
{
    public static (long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId) Of(
        long productId, long departmentId, string accountCd, long poolDepartmentId) =>
        (productId, departmentId, accountCd, poolDepartmentId);

    public static void AssertUnique(IEnumerable<(long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId)> keys)
    {
        var set = new HashSet<(long, long, string, long)>();
        foreach (var key in keys)
        {
            if (!set.Add(key))
                throw new InvalidOperationException(
                    $"Trùng khóa phân bổ PRODUCT={key.ProductId} DEPT={key.DepartmentId} " +
                    $"ACC={key.AccountCd} POOL_DEPT={key.PoolDepartmentId}.");
        }
    }
}

/// <summary>Bảo vệ ghi đè pool/detail đã xác nhận — logic thuần (map SP load_pool).</summary>
public static class AllocLoadGuard
{
    public static void EnsureCanOverwrite(
        string companyCd,
        long runId,
        int runVersion,
        int headerRunVersion,
        string? runStatus,
        string? allocationStepStatus)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || runId <= 0 || runVersion <= 0)
            throw new ArgumentException("COMPANY_CD / RUN_ID / RUN_VERSION không hợp lệ.");

        if (runVersion != headerRunVersion)
            throw new InvalidOperationException(
                "Chỉ được tính lại đúng RUN_VERSION hiện tại trên header RUN.");

        if (!GkRunStatus.AllowsPoolRecalc(runStatus, allocationStepStatus))
        {
            throw new InvalidOperationException(
                "Không được xóa/ghi đè pool khi RUN.STATUS=ALLOC_CONFIRMED/CONFIRMED " +
                "(không phụ thuộc STEP). Phải mở lại (REOPEN) trước khi tính lại.");
        }
    }

    /// <summary>
    /// Mô phỏng lỗi giữa chừng: nếu overwrite bị chặn, dữ liệu cũ phải còn nguyên
    /// (caller không gọi DELETE).
    /// </summary>
    public static bool WouldDeleteConfirmedData(string? runStatus, string? allocationStepStatus = null) =>
        !GkRunStatus.AllowsPoolRecalc(runStatus, allocationStepStatus);
}
