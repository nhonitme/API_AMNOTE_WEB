namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Chặn Confirm B4 ghi thật khi nguồn tính GT chưa authoritative từ DB.
/// Độc lập <c>EnableWrites</c> / WriteGate. Không tin WipDryRun client.
/// </summary>
public static class CostCalcConfirmSourceGuard
{
    public const string BlockReason =
        "Confirm B4 bị chặn: nguồn tính giá thành chưa authoritative từ DB " +
        "(load objects/source/receipt/opening pending). " +
        "Không ghi thật chỉ bằng WipDryRun client — guard độc lập EnableWrites.";

    /// <summary>
    /// Chỉ cho phép persist khi bundle nguồn đã load từ DB (không thay bằng dữ liệu tài chính client).
    /// </summary>
    public static void EnsureAuthoritativeFinancialSource(bool costCalcSourceLoadedFromDb)
    {
        if (!costCalcSourceLoadedFromDb)
            throw new InvalidOperationException(BlockReason);
    }
}
