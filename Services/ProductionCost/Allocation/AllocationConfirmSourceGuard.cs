namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Chặn Confirm B2 ghi thật khi nguồn pool/basis chưa authoritative từ DB.
/// Độc lập <c>EnableWrites</c> / WriteGate.
/// </summary>
public static class AllocationConfirmSourceGuard
{
    public const string BlockReason =
        "Confirm B2 bị chặn: nguồn pool/basis chưa authoritative từ DB (Preview-from-DB pending). " +
        "Không ghi thật chỉ bằng DryRun — guard độc lập EnableWrites.";

    /// <summary>
    /// Chỉ cho phép persist khi pool/basis đã load từ DB (không thay bằng dữ liệu tài chính client).
    /// </summary>
    public static void EnsureAuthoritativeFinancialSource(bool poolBasisLoadedFromDb)
    {
        if (!poolBasisLoadedFromDb)
            throw new InvalidOperationException(BlockReason);
    }
}
