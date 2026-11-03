using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Bút toán điều chỉnh 632 của B5.
/// Số tiền trên dòng luôn &gt;= 0, giống phiếu giá vốn kho (AMOUNT không âm).
/// Tăng giá vốn: Nợ 632 / Có 155. Giảm giá vốn: đảo Nợ 155 / Có 632.
/// Delta = 0 không sinh dòng.
/// </summary>
public static class ValuationCogsPosting
{
    public readonly record struct Line(string Debit, string Credit, decimal Amount);

    public static IReadOnlyList<Line> Build(IEnumerable<Cogs632PreviewLine> cogs)
    {
        ArgumentNullException.ThrowIfNull(cogs);
        var lines = new List<Line>();
        foreach (var row in cogs)
        {
            var delta = AllocationMath.RoundDigit(row.CogsDelta, 0);
            if (delta == 0)
                continue;
            if (delta > 0)
                lines.Add(new Line("632", "155", delta));
            else
                lines.Add(new Line("155", "632", -delta));
        }

        if (lines.Any(x => x.Amount < 0))
            throw new InvalidOperationException("Bút toán VALUATION_COGS không được có số tiền âm.");

        return lines;
    }

    public static decimal HeaderAmount(IReadOnlyList<Line> lines) =>
        AllocationMath.RoundDigit(lines.Sum(x => x.Amount), 0);
}

/// <summary>
/// CHIT_CD chứng từ GK_VALUATION theo convention khóa sổ:
/// header và dòng chi tiết dùng cùng mã (tối đa 30);
/// chitinfo_ext lưu cùng mã khi dài ≤ 20, nếu dài hơn thì LEFT 20 vì cột ext là varchar(20).
/// </summary>
public static class GkValuationChitCode
{
    public const int HeaderMaxLength = 30;
    public const int ExtMaxLength = 20;

    public static string NewHeader(DateTime utcNow)
    {
        var code = "C" + utcNow.ToString("yyyyMMddHHmmssfff") + "GV";
        if (code.Length > HeaderMaxLength)
            code = code[..HeaderMaxLength];
        return code;
    }

    public static string ForExt(string headerCode)
    {
        var code = RequireHeader(headerCode);
        return code.Length <= ExtMaxLength ? code : code[..ExtMaxLength];
    }

    public static string NewDetailCode(DateTime utcNow, int sort)
    {
        if (sort <= 0)
            throw new ArgumentOutOfRangeException(nameof(sort));
        var code = "D" + utcNow.ToString("yyyyMMddHHmmssfff") + sort.ToString("000000");
        return code.Length <= HeaderMaxLength ? code : code[..HeaderMaxLength];
    }

    public static string RequireHeader(string? headerCode)
    {
        var code = (headerCode ?? string.Empty).Trim();
        if (code.Length == 0 || code.Length > HeaderMaxLength)
            throw new InvalidOperationException("CHIT_CD header GK_VALUATION phải từ 1 đến 30 ký tự.");
        return code;
    }
}
