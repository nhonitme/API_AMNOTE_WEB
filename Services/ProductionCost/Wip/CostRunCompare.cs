using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>So sánh phiên CONFIRMED vs PREVIEW (unit cost / tổng GT / incurred).</summary>
public static class CostRunCompare
{
    public static IReadOnlyList<CostCompareLine> Compare(
        IEnumerable<WipCalcResult> confirmed,
        IEnumerable<WipCalcResult> preview)
    {
        var confMap = confirmed.ToDictionary(x => x.ProductId);
        var lines = new List<CostCompareLine>();

        foreach (var p in preview)
        {
            confMap.TryGetValue(p.ProductId, out var c);
            Add(lines, p, "TOTAL_COST_PRICE", c?.TotalCostPrice ?? 0, p.TotalCostPrice);
            Add(lines, p, "UNIT_COST", c?.UnitCost ?? 0, p.UnitCost);
            Add(lines, p, "TOTAL_AMOUNT_INCURRED", c?.TotalAmountIncurred ?? 0, p.TotalAmountIncurred);
            Add(lines, p, "CLOSING_INCOMPLETE_AMOUNT_154",
                c?.ClosingIncompleteAmount154 ?? 0, p.ClosingIncompleteAmount154);
        }

        return lines;
    }

    private static void Add(
        List<CostCompareLine> lines,
        WipCalcResult preview,
        string field,
        decimal confirmed,
        decimal previewValue)
    {
        if (confirmed == previewValue)
            return;
        lines.Add(new CostCompareLine
        {
            ProductId = preview.ProductId,
            ProductCd = preview.ProductCd,
            Field = field,
            ConfirmedValue = confirmed,
            PreviewValue = previewValue
        });
    }
}
