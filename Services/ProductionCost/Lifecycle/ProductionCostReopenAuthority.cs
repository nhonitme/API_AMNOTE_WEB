using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Quyền REOPEN qua menu G-K giản đơn (<c>/cost/simple-costing</c>) + <c>EDIT</c>
/// — cùng contract phân quyền AMnote (SysUserPermission / HasPermissionAsync).
/// Không tự chế cờ REOPEN riêng. Thiếu MENU_CODE cho route → chặn (blocker cấu hình).
/// </summary>
public sealed class ProductionCostReopenAuthority : IProductionCostReopenAuthority
{
    /// <summary>Route FE G-K giản đơn — map MENU_CODE theo công ty.</summary>
    public const string MenuRoutePath = "/cost/simple-costing";

    /// <summary>
    /// REOPEN là thao tác ghi trên kỳ đã khóa — dùng EDIT của menu G-K
    /// (SysUserPermission không có khóa REOPEN riêng).
    /// </summary>
    public const string PermissionKey = "EDIT";

    private readonly IMenuRepository _menus;

    public ProductionCostReopenAuthority(IMenuRepository menus)
    {
        _menus = menus ?? throw new ArgumentNullException(nameof(menus));
    }

    public async Task AssertCanConfirmReopenAsync(string companyCd, string? userId, string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD is required from authentication context.");

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException(
                "Người dùng không có quyền mở lại kỳ/bước G-K (thiếu user đăng nhập).");
        }

        ProductionCostWriteContext.EnsureDatabase(databaseName);

        // COMPANY_CD phải thuộc phiên đăng nhập — không tin body client.
        string sessionCompany;
        try
        {
            sessionCompany = Common.GetCompanyCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Không xác định COMPANY_CD phiên đăng nhập — từ chối Confirm REOPEN.", ex);
        }

        if (!string.Equals(sessionCompany.Trim(), companyCd.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "COMPANY_CD request không khớp phiên đăng nhập — từ chối Confirm REOPEN.");
        }

        // Database tenant: GetDatabaseName phải khớp databaseName đã resolve từ auth.
        string sessionDb;
        try
        {
            sessionDb = Common.GetDatabaseName();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Không xác định database phiên đăng nhập — từ chối Confirm REOPEN.", ex);
        }

        if (!string.Equals(sessionDb.Trim(), databaseName!.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Database request không khớp tenant phiên đăng nhập — từ chối Confirm REOPEN.");
        }

        var route = MenuRoutePath.TrimEnd('/');
        var menuCodes = (await _menus.GetMenuByCompanyAsync(companyCd.Trim()))
            .Where(menu => !menu.IS_DISABLED
                && string.Equals(
                    menu.ROUTE_PATH?.Trim().TrimEnd('/'),
                    route,
                    StringComparison.OrdinalIgnoreCase))
            .Select(menu => menu.MENU_CODE?.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (menuCodes.Count == 0)
        {
            throw new InvalidOperationException(
                "Confirm REOPEN bị chặn: chưa cấu hình MENU_CODE cho route /cost/simple-costing " +
                "(blocker phân quyền — không tự chế quyền REOPEN mới).");
        }

        foreach (var menuCode in menuCodes)
        {
            if (await Common.HasPermissionAsync(menuCode!, PermissionKey))
                return;
        }

        throw new InvalidOperationException(
            "Người dùng không có quyền mở lại kỳ/bước G-K (thiếu quyền EDIT trên menu giá thành giản đơn).");
    }
}
