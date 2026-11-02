namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Xây tiêu thức NVL (allocate_type=0) thuần — chống nhân đôi CHITDETAIL / orphan.
/// Logic đối chiếu <c>sp_pc_nvl_basis_build</c> (2B).
/// </summary>
public static class NvlBasisCalculator
{
    public const string ReasonNoBom = "NO_BOM";
    public const string ReasonSplitQtyZero = "SPLIT_QTY_ZERO";

    public sealed class RawLink
    {
        public long ProductId { get; set; }
        public long FinishedLineId { get; set; }
        public long OutputId { get; set; }
        public long? ChitdetailId { get; set; }
        public decimal ConfirmedQuantity { get; set; }
        public decimal OutputAmount { get; set; }
        public decimal DetailAmount { get; set; }
        public bool HasBom { get; set; } = true;
    }

    public sealed class BasisLine
    {
        public long ProductId { get; set; }
        public long OutputId { get; set; }
        public long? ChitdetailId { get; set; }
        public decimal ConfirmedQuantity { get; set; }
        public decimal SourceAmount { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal SplitRatio { get; set; }
    }

    public sealed class OrphanRow
    {
        public long? OutputId { get; set; }
        public long? ChitdetailId { get; set; }
        public decimal Amount { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
    }

    public sealed class Result
    {
        public IReadOnlyList<BasisLine> Lines { get; set; } = Array.Empty<BasisLine>();
        public IReadOnlyDictionary<long, decimal> AmountByProduct { get; set; } =
            new Dictionary<long, decimal>();
        public IReadOnlyList<OrphanRow> Orphans { get; set; } = Array.Empty<OrphanRow>();
    }

    public static Result Build(IReadOnlyList<RawLink> raw)
    {
        var orphans = new List<OrphanRow>();
        var working = new List<RawLink>();

        foreach (var row in raw)
        {
            if (!row.HasBom || row.OutputId <= 0)
            {
                orphans.Add(new OrphanRow
                {
                    OutputId = row.OutputId > 0 ? row.OutputId : null,
                    ChitdetailId = row.ChitdetailId,
                    Amount = Math.Round(row.DetailAmount, 2, MidpointRounding.AwayFromZero),
                    ReasonCode = ReasonNoBom
                });
                continue;
            }

            working.Add(row);
        }

        var outShare = working
            .GroupBy(x => x.OutputId)
            .ToDictionary(
                g => g.Key,
                g => (QtySum: g.Sum(x => x.ConfirmedQuantity), RowCnt: g.Count()));

        // ΣQ=0 và >1 TP cùng OUTPUT → orphan, không chia đều
        var dropOutputs = outShare
            .Where(kv => kv.Value.RowCnt > 1 && kv.Value.QtySum == 0m)
            .Select(kv => kv.Key)
            .ToHashSet();

        foreach (var row in working.Where(r => dropOutputs.Contains(r.OutputId)))
        {
            orphans.Add(new OrphanRow
            {
                OutputId = row.OutputId,
                ChitdetailId = row.ChitdetailId,
                Amount = Math.Round(ShareDetailAcrossOutputs(row, working), 2, MidpointRounding.AwayFromZero),
                ReasonCode = ReasonSplitQtyZero
            });
        }

        working = working.Where(r => !dropOutputs.Contains(r.OutputId)).ToList();

        var lines = new List<BasisLine>();
        foreach (var row in working)
        {
            var source = ShareDetailAcrossOutputs(row, working);
            var qtySum = outShare[row.OutputId].QtySum;
            decimal allocated;
            decimal ratio;
            if (qtySum > 0m)
            {
                ratio = row.ConfirmedQuantity / qtySum;
                allocated = source * ratio;
            }
            else
            {
                // 1 OUTPUT / 1 TP (hoặc còn 1 sau lọc): nhận đủ SOURCE
                ratio = 1m;
                allocated = source;
            }

            lines.Add(new BasisLine
            {
                ProductId = row.ProductId,
                OutputId = row.OutputId,
                ChitdetailId = row.ChitdetailId,
                ConfirmedQuantity = row.ConfirmedQuantity,
                SourceAmount = Math.Round(source, 2, MidpointRounding.AwayFromZero),
                AllocatedAmount = Math.Round(allocated, 2, MidpointRounding.AwayFromZero),
                SplitRatio = ratio
            });
        }

        var byProduct = lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Sum(x => x.AllocatedAmount), 0, MidpointRounding.AwayFromZero));

        return new Result
        {
            Lines = lines,
            AmountByProduct = byProduct,
            Orphans = orphans
        };
    }

    /// <summary>
    /// Một CHITDETAIL_ID gắn nhiều OUTPUT_ID: chia AMOUNT theo tỷ lệ AMOUNT_CC output (không nhân đôi).
    /// </summary>
    private static decimal ShareDetailAcrossOutputs(RawLink row, IReadOnlyList<RawLink> all)
    {
        if (row.ChitdetailId is null or <= 0)
            return row.DetailAmount;

        var outputs = all
            .Where(x => x.ChitdetailId == row.ChitdetailId)
            .GroupBy(x => x.OutputId)
            .Select(g => g.Max(x => x.OutputAmount))
            .ToList();
        var outAmtSum = outputs.Sum();
        if (outAmtSum <= 0m)
            return row.DetailAmount;

        return row.DetailAmount * (row.OutputAmount / outAmtSum);
    }
}
