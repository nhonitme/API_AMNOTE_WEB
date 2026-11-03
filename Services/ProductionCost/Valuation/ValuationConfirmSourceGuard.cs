namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Chặn Confirm B5 ghi thật khi nguồn định giá / kho / chứng từ chưa authoritative từ DB,
/// hoặc khi chưa có UPDATE INPUT_ID+COMPANY_CD an toàn.
/// Độc lập <c>EnableWrites</c> / WriteGate. Không tin ValuationDryRun client.
/// </summary>
public static class ValuationConfirmSourceGuard
{
    public const string BlockReason =
        "Confirm B5 bị chặn: nguồn receipt / inventory movements / valuation / chứng từ KT " +
        "chưa authoritative từ DB (load + đối chiếu pending). " +
        "Không ghi thật chỉ bằng ValuationDryRun client — guard độc lập EnableWrites.";

    public const string InputIdApplyNotReadyReason =
        "Confirm B5 bị chặn: chưa có repository/SQL cập nhật giá nhập theo INPUT_ID+COMPANY_CD an toàn. " +
        "Không dùng apply_inventory_update_in_price (PRODUCT_CD). " +
        "Chuẩn bị implementation — Confirm thật giữ blocked đến khi DEV bàn giao SQL INPUT_ID.";

    /// <summary>
    /// Chỉ cho phép persist khi bundle nguồn đã load/đối chiếu từ DB
    /// (không thay bằng ValuationDryRun tài chính client).
    /// </summary>
    public static void EnsureAuthoritativeFinancialSource(bool valuationSourceLoadedFromDb)
    {
        if (!valuationSourceLoadedFromDb)
            throw new InvalidOperationException(BlockReason);
    }

    /// <summary>
    /// Chặn apply kho khi capability INPUT_ID chưa sẵn sàng trên repository.
    /// </summary>
    public static void EnsureInputIdSafeApplyReady(bool supportsInputIdSafeInventoryUpdate)
    {
        if (!supportsInputIdSafeInventoryUpdate)
            throw new InvalidOperationException(InputIdApplyNotReadyReason);
    }
}
