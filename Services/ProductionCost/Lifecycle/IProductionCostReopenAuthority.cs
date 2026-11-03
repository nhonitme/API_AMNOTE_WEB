namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Quyền Confirm REOPEN — dùng hệ thống phân quyền AMnote hiện có
/// (<c>Common.HasPermissionAsync</c> + MENU_CODE theo route), không tin
/// <c>ReopenDryRun.HasPermission</c>, không mặc định mọi user đã login đều được REOPEN.
/// </summary>
public interface IProductionCostReopenAuthority
{
    /// <summary>
    /// Assert user có quyền REOPEN trên COMPANY_CD / database của phiên đăng nhập.
    /// Ném <see cref="InvalidOperationException"/> khi thiếu quyền hoặc tenant lệch.
    /// </summary>
    Task AssertCanConfirmReopenAsync(string companyCd, string? userId, string? databaseName);
}
