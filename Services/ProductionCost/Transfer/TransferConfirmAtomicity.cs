namespace API_AMNOTE_WEB.Services.ProductionCost.Transfer;

/// <summary>
/// TX boundary B3: PeriodLock COGS SPs phải chạy trên cùng DapperSession G-K.
/// Không dual-commit PeriodLock rồi G-K STEP/history.
/// </summary>
public static class TransferConfirmAtomicity
{
    /// <summary>
    /// True vì <c>sp_period_lock_cogs_*</c> không START/COMMIT — caller C# sở hữu TX.
    /// Nếu false: Confirm B3 phải blocked.
    /// </summary>
    public const bool SupportsSharedGkSession = true;

    public static void EnsureSharedSessionAvailable()
    {
        if (!SupportsSharedGkSession)
        {
            throw new InvalidOperationException(
                "Confirm B3 bị chặn: chưa bảo đảm atomic transaction PeriodLock+G-K trên cùng DapperSession. " +
                "Không dùng hai commit tách biệt.");
        }
    }
}
