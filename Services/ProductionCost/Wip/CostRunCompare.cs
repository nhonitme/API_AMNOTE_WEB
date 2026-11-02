using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// So sánh phiên CONFIRMED vs PREVIEW theo khóa (PRODUCT_ID, DEPARTMENT_ID).
/// </summary>
public static class CostRunCompare
{
    public static class Presence
    {
        public const string Both = "BOTH";
        public const string ConfirmedOnly = "CONFIRMED_ONLY";
        public const string PreviewOnly = "PREVIEW_ONLY";
    }

    private static readonly string[] ValueFields =
    {
        "TOTAL_COST_PRICE",
        "UNIT_COST",
        "TOTAL_AMOUNT_INCURRED",
        "CLOSING_INCOMPLETE_AMOUNT_154"
    };

    public static (long ProductId, long DepartmentId) Key(WipCalcResult row) =>
        (row.ProductId, row.DepartmentId);

    public static IReadOnlyList<CostCompareLine> Compare(
        IEnumerable<WipCalcResult> confirmed,
        IEnumerable<WipCalcResult> preview)
    {
        var confMap = ToUniqueMap(confirmed, "CONFIRMED");
        var prevMap = ToUniqueMap(preview, "PREVIEW");
        var allKeys = confMap.Keys.Union(prevMap.Keys).OrderBy(k => k.ProductId).ThenBy(k => k.DepartmentId);
        var lines = new List<CostCompareLine>();

        foreach (var key in allKeys)
        {
            confMap.TryGetValue(key, out var c);
            prevMap.TryGetValue(key, out var p);

            if (c != null && p == null)
            {
                lines.Add(RowPresence(c, Presence.ConfirmedOnly, c.TotalCostPrice, 0));
                continue;
            }

            if (c == null && p != null)
            {
                lines.Add(RowPresence(p, Presence.PreviewOnly, 0, p.TotalCostPrice));
                continue;
            }

            // BOTH
            foreach (var field in ValueFields)
            {
                var cv = FieldValue(c!, field);
                var pv = FieldValue(p!, field);
                if (cv == pv)
                    continue;
                lines.Add(new CostCompareLine
                {
                    ProductId = key.ProductId,
                    DepartmentId = key.DepartmentId,
                    ProductCd = p!.ProductCd ?? c!.ProductCd,
                    Field = field,
                    Presence = Presence.Both,
                    ConfirmedValue = cv,
                    PreviewValue = pv
                });
            }
        }

        return lines;
    }

    private static Dictionary<(long ProductId, long DepartmentId), WipCalcResult> ToUniqueMap(
        IEnumerable<WipCalcResult> rows,
        string side)
    {
        var map = new Dictionary<(long, long), WipCalcResult>();
        foreach (var row in rows)
        {
            var key = Key(row);
            if (!map.TryAdd(key, row))
            {
                throw new InvalidOperationException(
                    $"Trùng khóa so sánh {side}: PRODUCT={key.ProductId} DEPT={key.DepartmentId}.");
            }
        }

        return map;
    }

    private static CostCompareLine RowPresence(
        WipCalcResult row,
        string presence,
        decimal confirmed,
        decimal preview) =>
        new()
        {
            ProductId = row.ProductId,
            DepartmentId = row.DepartmentId,
            ProductCd = row.ProductCd,
            Field = "ROW",
            Presence = presence,
            ConfirmedValue = confirmed,
            PreviewValue = preview
        };

    private static decimal FieldValue(WipCalcResult row, string field) => field switch
    {
        "TOTAL_COST_PRICE" => row.TotalCostPrice,
        "UNIT_COST" => row.UnitCost,
        "TOTAL_AMOUNT_INCURRED" => row.TotalAmountIncurred,
        "CLOSING_INCOMPLETE_AMOUNT_154" => row.ClosingIncompleteAmount154,
        _ => 0
    };
}
