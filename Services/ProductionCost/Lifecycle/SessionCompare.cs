using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.Inventory;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// So sánh phiên CONFIRMED vs PREVIEW/REOPENED: WIP, giá nhập INPUT_ID, định giá theo khóa Kho.
/// </summary>
public static class SessionCompare
{
    public static SessionCompareResult Compare(SessionCompareDryRunPayload dry)
    {
        var wip = CostRunCompare.Compare(
            dry.ConfirmedWip ?? Array.Empty<WipCalcResult>(),
            dry.PreviewWip ?? Array.Empty<WipCalcResult>());

        var receipts = CompareReceipts(
            dry.ConfirmedReceipts ?? Array.Empty<ReceiptPricePrepLine>(),
            dry.PreviewReceipts ?? Array.Empty<ReceiptPricePrepLine>());

        var valuation = CompareValuation(
            dry.ConfirmedValuationGroups ?? Array.Empty<ValuationAverageResult>(),
            dry.PreviewValuationGroups ?? Array.Empty<ValuationAverageResult>());

        return new SessionCompareResult
        {
            ConfirmedRunVersion = dry.ConfirmedRunVersion,
            PreviewRunVersion = dry.PreviewRunVersion,
            WipLines = wip,
            ReceiptLines = receipts,
            ValuationLines = valuation,
            HasDifferences = wip.Count > 0 || receipts.Count > 0 || valuation.Count > 0,
            Message =
                $"So sánh CONFIRMED v{dry.ConfirmedRunVersion} vs PREVIEW/REOPENED v{dry.PreviewRunVersion}."
        };
    }

    public static IReadOnlyList<SessionCompareLine> CompareReceipts(
        IEnumerable<ReceiptPricePrepLine> confirmed,
        IEnumerable<ReceiptPricePrepLine> preview)
    {
        var cMap = ToUnique(confirmed, r => r.InputId, "CONFIRMED receipt");
        var pMap = ToUnique(preview, r => r.InputId, "PREVIEW receipt");
        var keys = cMap.Keys.Union(pMap.Keys).OrderBy(k => k);
        var lines = new List<SessionCompareLine>();

        foreach (var inputId in keys)
        {
            cMap.TryGetValue(inputId, out var c);
            pMap.TryGetValue(inputId, out var p);
            if (c != null && p == null)
            {
                lines.Add(Line("RECEIPT", inputId, SessionComparePresence.ConfirmedOnly, c.NewAmount, 0, c.ProductId));
                continue;
            }

            if (c == null && p != null)
            {
                lines.Add(Line("RECEIPT", inputId, SessionComparePresence.PreviewOnly, 0, p.NewAmount, p.ProductId));
                continue;
            }

            if (c!.NewUnitPrice != p!.NewUnitPrice)
            {
                lines.Add(new SessionCompareLine
                {
                    Scope = "RECEIPT",
                    Key = $"INPUT_ID={inputId}",
                    Field = "NEW_UNIT_PRICE",
                    Presence = SessionComparePresence.Both,
                    ProductId = p.ProductId,
                    ConfirmedValue = c.NewUnitPrice,
                    PreviewValue = p.NewUnitPrice
                });
            }

            if (c.NewAmount != p.NewAmount)
            {
                lines.Add(new SessionCompareLine
                {
                    Scope = "RECEIPT",
                    Key = $"INPUT_ID={inputId}",
                    Field = "NEW_AMOUNT",
                    Presence = SessionComparePresence.Both,
                    ProductId = p.ProductId,
                    ConfirmedValue = c.NewAmount,
                    PreviewValue = p.NewAmount
                });
            }
        }

        return lines;
    }

    public static IReadOnlyList<SessionCompareLine> CompareValuation(
        IEnumerable<ValuationAverageResult> confirmed,
        IEnumerable<ValuationAverageResult> preview)
    {
        var cMap = ToUnique(confirmed, ValuationKey, "CONFIRMED valuation");
        var pMap = ToUnique(preview, ValuationKey, "PREVIEW valuation");
        var keys = cMap.Keys.Union(pMap.Keys).OrderBy(k => k.ProductCd).ThenBy(k => k.StoreCd).ThenBy(k => k.ChitType);
        var lines = new List<SessionCompareLine>();

        foreach (var key in keys)
        {
            cMap.TryGetValue(key, out var c);
            pMap.TryGetValue(key, out var p);
            var keyText =
                $"{key.ProductCd}|{key.ProductName}|{key.StoreCd}|{key.StoreName}|{key.ChitType}";

            if (c != null && p == null)
            {
                lines.Add(new SessionCompareLine
                {
                    Scope = "VALUATION",
                    Key = keyText,
                    Field = "ROW",
                    Presence = SessionComparePresence.ConfirmedOnly,
                    ProductId = c.ProductId,
                    ProductCd = c.ProductCd,
                    ConfirmedValue = c.OutputAmount,
                    PreviewValue = 0
                });
                continue;
            }

            if (c == null && p != null)
            {
                lines.Add(new SessionCompareLine
                {
                    Scope = "VALUATION",
                    Key = keyText,
                    Field = "ROW",
                    Presence = SessionComparePresence.PreviewOnly,
                    ProductId = p.ProductId,
                    ProductCd = p.ProductCd,
                    ConfirmedValue = 0,
                    PreviewValue = p.OutputAmount
                });
                continue;
            }

            AddIfDiff(lines, keyText, "OUTPUT_AMOUNT", c!.ProductId, c.ProductCd, c.OutputAmount, p!.OutputAmount);
            AddIfDiff(lines, keyText, "ENDING_AMOUNT", c.ProductId, c.ProductCd, c.EndingAmount, p.EndingAmount);
            AddIfDiff(lines, keyText, "ENDING_AVG_UNIT", c.ProductId, c.ProductCd,
                c.EndingAverageUnitCost, p.EndingAverageUnitCost);
            AddIfDiff(lines, keyText, "OUTPUT_AVG_UNIT", c.ProductId, c.ProductCd,
                c.OutputAverageUnitCost, p.OutputAverageUnitCost);
        }

        return lines;
    }

    private static InventoryOutPriceEngine.GroupKey ValuationKey(ValuationAverageResult g) =>
        new(
            g.ProductCd ?? string.Empty,
            g.Group.ProductName ?? string.Empty,
            g.StoreCd ?? string.Empty,
            g.Group.StoreName ?? string.Empty,
            g.ChitType ?? string.Empty);

    private static void AddIfDiff(
        List<SessionCompareLine> lines,
        string key,
        string field,
        long productId,
        string? productCd,
        decimal confirmed,
        decimal preview)
    {
        if (confirmed == preview)
            return;
        lines.Add(new SessionCompareLine
        {
            Scope = "VALUATION",
            Key = key,
            Field = field,
            Presence = SessionComparePresence.Both,
            ProductId = productId,
            ProductCd = productCd,
            ConfirmedValue = confirmed,
            PreviewValue = preview
        });
    }

    private static SessionCompareLine Line(
        string scope, long inputId, string presence, decimal confirmed, decimal preview, long productId) =>
        new()
        {
            Scope = scope,
            Key = $"INPUT_ID={inputId}",
            Field = "ROW",
            Presence = presence,
            ProductId = productId,
            ConfirmedValue = confirmed,
            PreviewValue = preview
        };

    private static Dictionary<TKey, T> ToUnique<T, TKey>(
        IEnumerable<T> rows,
        Func<T, TKey> keySelector,
        string side) where TKey : notnull
    {
        var map = new Dictionary<TKey, T>();
        foreach (var row in rows)
        {
            var key = keySelector(row);
            if (!map.TryAdd(key, row))
                throw new InvalidOperationException($"Trùng khóa so sánh {side}: {key}.");
        }

        return map;
    }
}
