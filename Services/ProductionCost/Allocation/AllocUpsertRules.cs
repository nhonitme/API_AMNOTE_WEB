using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Quy tắc UPSERT pool/detail theo UNIQUE nghiệp vụ (không gồm ISDEL).
/// Soft-delete rồi INSERT cùng khóa → duplicate key; phải ON DUPLICATE KEY UPDATE.
/// </summary>
public static class AllocUpsertRules
{
    public const string PersistStrategy = "UPSERT_ON_BUSINESS_KEY";

    public static (long RunId, int RunVersion, string AccountCd, long DepartmentId) PoolKey(
        long runId, int runVersion, string accountCd, long departmentId) =>
        (runId, runVersion, (accountCd ?? string.Empty).Trim(), departmentId);

    public static (long RunId, int RunVersion, long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId)
        DetailKey(long runId, int runVersion, long productId, long departmentId, string accountCd, long poolDepartmentId) =>
        (runId, runVersion, productId, departmentId, (accountCd ?? string.Empty).Trim(), poolDepartmentId);

    /// <summary>
    /// Chặn ghi đè khi RUN đã ALLOC_CONFIRMED/CONFIRMED; chỉ đúng RUN_VERSION header.
    /// </summary>
    public static void EnsureCanPersist(
        string companyCd,
        long runId,
        int runVersion,
        int lockedRunVersion,
        string? runStatus,
        string? allocationStepStatus = null)
    {
        AllocLoadGuard.EnsureCanOverwrite(
            companyCd, runId, runVersion, lockedRunVersion, runStatus, allocationStepStatus);
    }

    /// <summary>
    /// Mô phỏng UNIQUE MySQL (bỏ ISDEL): UPSERT luôn thắng; INSERT thuần vào khóa đã có → lỗi.
    /// Dùng unit test — kể cả hàng ISDEL='1'.
    /// </summary>
    public static void UpsertPoolRow(
        IDictionary<(long RunId, int RunVersion, string AccountCd, long DepartmentId), AllocPoolStoreRow> store,
        long runId,
        int runVersion,
        AllocationPoolInput row,
        bool useUpsert)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(row);
        var key = PoolKey(runId, runVersion, row.AccountCd, row.DepartmentId);
        if (!useUpsert && store.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate entry for key uk_pc_pool " +
                $"({runId},{runVersion},{row.AccountCd},{row.DepartmentId}) — UNIQUE không chứa ISDEL.");
        }

        store[key] = new AllocPoolStoreRow
        {
            AccountCd = key.AccountCd,
            DepartmentId = key.DepartmentId,
            TotalAmount = row.TotalAmount,
            CreditAmount = row.CreditAmount,
            DebitAmount154 = row.DebitAmount154,
            Reduction = row.Reduction,
            AllocationCosts = row.AllocationCosts,
            DepartmentCd = row.DepartmentCd,
            IsDel = "0"
        };
    }

    public static void UpsertDetailRow(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId), AllocDetailStoreRow> store,
        long runId,
        int runVersion,
        AllocationLineResult line,
        bool useUpsert)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(line);
        var key = DetailKey(
            runId, runVersion, line.ProductId, line.DepartmentId, line.AccountCd, line.PoolDepartmentId);
        if (!useUpsert && store.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate entry for key uk_pc_alloc_detail " +
                $"({runId},{runVersion},{line.ProductId},{line.DepartmentId},{line.AccountCd},{line.PoolDepartmentId}) " +
                "— UNIQUE không chứa ISDEL.");
        }

        store[key] = new AllocDetailStoreRow
        {
            ProductId = line.ProductId,
            DepartmentId = line.DepartmentId,
            PoolDepartmentId = line.PoolDepartmentId,
            AccountCd = key.AccountCd,
            AllocatedAmount = line.AllocatedAmount,
            IsLastRow = line.IsLastRow ? "1" : "0",
            IsDel = "0"
        };
    }

    /// <summary>
    /// Sau UPSERT: soft-delete mọi khóa cùng RUN_VERSION không còn trong kết quả mới.
    /// Không đụng RUN_VERSION khác.
    /// </summary>
    public static int SoftDeleteMissingPoolKeys(
        IDictionary<(long RunId, int RunVersion, string AccountCd, long DepartmentId), AllocPoolStoreRow> store,
        long runId,
        int runVersion,
        IReadOnlyCollection<(string AccountCd, long DepartmentId)> keepKeys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keepKeys);
        var keep = keepKeys
            .Select(k => PoolKey(runId, runVersion, k.AccountCd, k.DepartmentId))
            .ToHashSet();

        var removed = 0;
        foreach (var key in store.Keys.ToList())
        {
            if (key.RunId != runId || key.RunVersion != runVersion)
                continue;
            if (keep.Contains(key))
                continue;
            if (store[key].IsDel == "1")
                continue;
            store[key].IsDel = "1";
            removed++;
        }

        return removed;
    }

    public static int SoftDeleteMissingDetailKeys(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId), AllocDetailStoreRow> store,
        long runId,
        int runVersion,
        IReadOnlyCollection<(long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId)> keepKeys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keepKeys);
        var keep = keepKeys
            .Select(k => DetailKey(runId, runVersion, k.ProductId, k.DepartmentId, k.AccountCd, k.PoolDepartmentId))
            .ToHashSet();

        var removed = 0;
        foreach (var key in store.Keys.ToList())
        {
            if (key.RunId != runId || key.RunVersion != runVersion)
                continue;
            if (keep.Contains(key))
                continue;
            if (store[key].IsDel == "1")
                continue;
            store[key].IsDel = "1";
            removed++;
        }

        return removed;
    }
}

public sealed class AllocPoolStoreRow
{
    public string AccountCd { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal DebitAmount154 { get; set; }
    public decimal Reduction { get; set; }
    public decimal AllocationCosts { get; set; }
    public string IsDel { get; set; } = "0";
}

public sealed class AllocDetailStoreRow
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public long PoolDepartmentId { get; set; }
    public string AccountCd { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public string IsLastRow { get; set; } = "0";
    public string IsDel { get; set; } = "0";
}
