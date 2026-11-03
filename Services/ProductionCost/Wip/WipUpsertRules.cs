using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Quy tắc UPSERT source/wip/receipt theo UNIQUE nghiệp vụ (không gồm ISDEL).
/// Soft-delete rồi INSERT cùng khóa → duplicate key; phải ON DUPLICATE KEY UPDATE.
/// </summary>
public static class WipUpsertRules
{
    public const string PersistStrategy = "UPSERT_ON_BUSINESS_KEY";

    public static (long RunId, int RunVersion, long ProductId, long DepartmentId, string SourceKind) SourceKey(
        long runId, int runVersion, long productId, long departmentId, string sourceKind) =>
        (runId, runVersion, productId, departmentId, (sourceKind ?? string.Empty).Trim());

    public static (long RunId, int RunVersion, long ProductId, long DepartmentId) WipKey(
        long runId, int runVersion, long productId, long departmentId) =>
        (runId, runVersion, productId, departmentId);

    public static (long RunId, int RunVersion, long InputId) ReceiptKey(
        long runId, int runVersion, long inputId) =>
        (runId, runVersion, inputId);

    public static void EnsureCanPersist(
        string companyCd,
        long runId,
        int runVersion,
        int lockedRunVersion,
        string? runStatus,
        bool targetingConfirmedVersion = false)
    {
        WipRecalcGuard.EnsureCanOverwriteWip(
            companyCd, runId, runVersion, lockedRunVersion, runStatus, targetingConfirmedVersion);
    }

    public static void UpsertSourceRow(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId, string SourceKind), WipSourceStoreRow> store,
        long runId,
        int runVersion,
        CostSourceLine row,
        bool useUpsert)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(row);
        CostSourceRules.AssertNoTransferInIncurred(new[] { row });

        var key = SourceKey(runId, runVersion, row.ProductId, row.DepartmentId, row.SourceKind);
        if (!useUpsert && store.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate entry for key uk_pc_source " +
                $"({runId},{runVersion},{row.ProductId},{row.DepartmentId},{row.SourceKind}) — UNIQUE không chứa ISDEL.");
        }

        store[key] = new WipSourceStoreRow
        {
            ProductId = row.ProductId,
            DepartmentId = row.DepartmentId,
            SourceKind = key.SourceKind,
            Amount = row.Amount,
            IsDel = "0"
        };
    }

    public static void UpsertWipRow(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId), WipCalcStoreRow> store,
        long runId,
        int runVersion,
        WipCalcResult row,
        bool useUpsert)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(row);
        var key = WipKey(runId, runVersion, row.ProductId, row.DepartmentId);
        if (!useUpsert && store.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate entry for key uk_pc_wip " +
                $"({runId},{runVersion},{row.ProductId},{row.DepartmentId}) — UNIQUE không chứa ISDEL.");
        }

        store[key] = new WipCalcStoreRow
        {
            ProductId = row.ProductId,
            DepartmentId = row.DepartmentId,
            ProductCd = row.ProductCd,
            DepartmentCd = row.DepartmentCd,
            TotalCostPrice = row.TotalCostPrice,
            UnitCost = row.UnitCost,
            Quantity = row.Quantity,
            Amount154Direct = row.Amount154Direct,
            TotalAmountIncurred = row.TotalAmountIncurred,
            ClosingIncompleteAmount154 = row.ClosingIncompleteAmount154,
            IsDel = "0"
        };
    }

    public static void UpsertReceiptRow(
        IDictionary<(long RunId, int RunVersion, long InputId), WipReceiptStoreRow> store,
        long runId,
        int runVersion,
        ReceiptPricePrepLine row,
        bool useUpsert)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(row);
        if (row.InputId <= 0)
            throw new ArgumentException("INPUT_ID không hợp lệ.");
        if (row.IsApplied)
            throw new InvalidOperationException("B4 cấm persist receipt với IsApplied=true.");

        var key = ReceiptKey(runId, runVersion, row.InputId);
        if (!useUpsert && store.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate entry for key uk_pc_receipt " +
                $"({runId},{runVersion},{row.InputId}) — UNIQUE không chứa ISDEL.");
        }

        store[key] = new WipReceiptStoreRow
        {
            ProductId = row.ProductId,
            DepartmentId = row.DepartmentId,
            InputId = row.InputId,
            InventoryId = row.InventoryId,
            Qty = row.Qty,
            OldUnitPrice = row.OldUnitPrice,
            NewUnitPrice = row.NewUnitPrice,
            OldAmount = row.OldAmount,
            NewAmount = row.NewAmount,
            DeltaAmount = row.DeltaAmount,
            IsApplied = "0",
            IsDel = "0"
        };
    }

    public static int SoftDeleteMissingSourceKeys(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId, string SourceKind), WipSourceStoreRow> store,
        long runId,
        int runVersion,
        IReadOnlyCollection<(long ProductId, long DepartmentId, string SourceKind)> keepKeys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keepKeys);
        var keep = keepKeys
            .Select(k => SourceKey(runId, runVersion, k.ProductId, k.DepartmentId, k.SourceKind))
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

    public static int SoftDeleteMissingWipKeys(
        IDictionary<(long RunId, int RunVersion, long ProductId, long DepartmentId), WipCalcStoreRow> store,
        long runId,
        int runVersion,
        IReadOnlyCollection<(long ProductId, long DepartmentId)> keepKeys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keepKeys);
        var keep = keepKeys
            .Select(k => WipKey(runId, runVersion, k.ProductId, k.DepartmentId))
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

    public static int SoftDeleteMissingReceiptKeys(
        IDictionary<(long RunId, int RunVersion, long InputId), WipReceiptStoreRow> store,
        long runId,
        int runVersion,
        IReadOnlyCollection<long> keepInputIds)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keepInputIds);
        var keep = keepInputIds
            .Select(id => ReceiptKey(runId, runVersion, id))
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

    /// <summary>
    /// Từ Amount* trên product (khi không có Sources riêng) — mỗi kind &gt; 0 thành 1 dòng source.
    /// </summary>
    public static IReadOnlyList<CostSourceLine> SourcesFromProductAmounts(IEnumerable<WipCalcInput> products)
    {
        var list = new List<CostSourceLine>();
        foreach (var p in products)
        {
            AddIfPositive(list, p, ProductionCostSourceKind.Src621, p.Amount621);
            AddIfPositive(list, p, ProductionCostSourceKind.Src6212, p.Amount6212);
            AddIfPositive(list, p, ProductionCostSourceKind.Src622Alloc, p.Amount622);
            AddIfPositive(list, p, ProductionCostSourceKind.Src627Alloc, p.Amount627);
            AddIfPositive(list, p, ProductionCostSourceKind.Src154Direct, p.Amount154Direct);
        }

        return list;
    }

    private static void AddIfPositive(
        List<CostSourceLine> list, WipCalcInput p, string kind, decimal amount)
    {
        if (amount == 0)
            return;
        list.Add(new CostSourceLine
        {
            ProductId = p.ProductId,
            DepartmentId = p.DepartmentId,
            SourceKind = kind,
            Amount = amount
        });
    }
}

public sealed class WipSourceStoreRow
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string IsDel { get; set; } = "0";
}

public sealed class WipCalcStoreRow
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal TotalCostPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount154Direct { get; set; }
    public decimal TotalAmountIncurred { get; set; }
    public decimal ClosingIncompleteAmount154 { get; set; }
    public string IsDel { get; set; } = "0";
}

public sealed class WipReceiptStoreRow
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public long InputId { get; set; }
    public long? InventoryId { get; set; }
    public decimal Qty { get; set; }
    public decimal OldUnitPrice { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal DeltaAmount { get; set; }
    public string IsApplied { get; set; } = "0";
    public string IsDel { get; set; } = "0";
}
