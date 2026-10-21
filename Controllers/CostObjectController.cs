using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    /// <summary>
    /// Khai báo đối tượng tập hợp chi phí từ thành phẩm.
    /// PRODUCT_CD == DEPARTMENT_CD trong cùng COMPANY_CD.
    /// Giữ nguyên danh mục product_info và department_info hiện hữu.
    /// </summary>
    [ApiController]
    [Authorize]
    public sealed class CostObjectController : BaseApiController
    {
        private const string CostObjectRoutePath = "/cost/cost-object";
        private const string DefaultFinishedGoodsAccountPrefix = "155";

        private readonly IProductInfoService _products;
        private readonly IDepartmentInfoService _departments;
        private readonly IMenuRepository _menus;
        private readonly ILogger<CostObjectController> _logger;

        public CostObjectController(
            IProductInfoService products,
            IDepartmentInfoService departments,
            IMenuRepository menus,
            ILogger<CostObjectController> logger)
        {
            _products = products;
            _departments = departments;
            _menus = menus;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetOverview([FromQuery] string? divisionPrefix = null)
        {
            var companyCd = Common.GetCompanyCode();
            if (!await HasCostObjectPermissionAsync(companyCd, "VIEW"))
                return Forbidden("Bạn không có quyền xem chức năng khai báo đối tượng tập hợp chi phí.");

            var prefix = NormalizeDivisionPrefix(divisionPrefix);
            if (prefix == null)
                return ValidationError("Mã tài khoản thành phẩm không hợp lệ (1-20 chữ số).");

            var products = (await _products.GetListAsync(companyCd))
                .Where(p => IsFinishedGood(p, prefix))
                .GroupBy(p => p.PRODUCT_CD.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.PRODUCT_CD, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var departments = (await _departments.GetListAsync(companyCd))
                .Where(d => d.ISDEL != "1" && !string.IsNullOrWhiteSpace(d.DEPARTMENT_CD))
                .GroupBy(d => d.DEPARTMENT_CD.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var rows = products.Select(p =>
            {
                var code = p.PRODUCT_CD.Trim();
                departments.TryGetValue(code, out var department);
                return new CostObjectSetupItemDto
                {
                    PRODUCT_ID = p.PRODUCT_ID,
                    PRODUCT_CD = code,
                    PRODUCT_NM_VIET = p.PRODUCT_NM_VIET,
                    PRODUCT_NM_ENG = p.PRODUCT_NM_ENG,
                    PRODUCT_NM_KOR = p.PRODUCT_NM_KOR,
                    PRODUCT_NM_CHINA = p.PRODUCT_NM_CHINA,
                    DIVISION = p.DIVISION,
                    DEPARTMENT_ID = department?.DEPARTMENT_ID,
                    DEPARTMENT_CD = department?.DEPARTMENT_CD,
                    DEP_NAME_VIET = department?.DEP_NAME_VIET,
                    DEP_NAME_ENG = department?.DEP_NAME_ENG,
                    DEP_NAME_KOR = department?.DEP_NAME_KOR,
                    DEP_NAME_CHINA = department?.DEP_NAME_CHINA,
                    STATUS = department == null ? "MISSING" : "REGISTERED"
                };
            }).ToList();

            return Success(new CostObjectSetupOverviewDto
            {
                DIVISION_PREFIX = prefix,
                TOTAL = rows.Count,
                REGISTERED = rows.Count(x => x.STATUS == "REGISTERED"),
                MISSING = rows.Count(x => x.STATUS == "MISSING"),
                ITEMS = rows
            });
        }

        /// <summary>
        /// Tạo đối tượng thiếu theo mã thành phẩm. Có thể gửi nhiều ID; thao tác lặp là an toàn.
        /// Không sửa tên/mã của các đối tượng đã tồn tại.
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromBody] CostObjectSyncRequest? request)
        {
            var companyCd = Common.GetCompanyCode();
            if (!await HasCostObjectPermissionAsync(companyCd, "ADD"))
                return Forbidden("Bạn không có quyền tạo đối tượng tập hợp chi phí.");

            if (request?.ProductIds == null || request.ProductIds.Count == 0)
                return ValidationError("Vui lòng chọn ít nhất một thành phẩm.");

            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count > 200 || ids.Any(id => id <= 0))
                return ValidationError("Mỗi lần đồng bộ chọn từ 1 đến 200 thành phẩm hợp lệ.");

            var prefix = NormalizeDivisionPrefix(request.DivisionPrefix);
            if (prefix == null)
                return ValidationError("Mã tài khoản thành phẩm không hợp lệ (1-20 chữ số).");

            var userId = Common.GetUserId();
            var eligibleProducts = (await _products.GetListAsync(companyCd))
                .Where(p => IsFinishedGood(p, prefix))
                .ToDictionary(p => p.PRODUCT_ID);

            var result = new CostObjectSyncResultDto();
            foreach (var productId in ids)
            {
                if (!eligibleProducts.TryGetValue(productId, out var product))
                {
                    result.FAILED++;
                    result.ITEMS.Add(new CostObjectSyncItemDto
                    {
                        PRODUCT_ID = productId,
                        STATUS = "FAILED",
                        MESSAGE = "Không tìm thấy thành phẩm hợp lệ theo tài khoản đã chọn."
                    });
                    continue;
                }

                var code = product.PRODUCT_CD.Trim();
                // If linking fails after creating a department, report FAILED.
                // The next synchronization can safely attach that existing department.
                var linkAttempted = false;
                try
                {
                    // Match within the current company; never attach an unrelated department.
                    var existing = (await _departments.GetListAsync(companyCd, departmentCd: code))
                        .FirstOrDefault(d => d.ISDEL != "1" &&
                            string.Equals(d.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        linkAttempted = true;
                        await _products.LinkDepartmentAsync(
                            companyCd, userId, productId, code, existing.DEPARTMENT_ID, prefix);

                        result.EXISTS++;
                        result.ITEMS.Add(new CostObjectSyncItemDto
                        {
                            PRODUCT_ID = productId,
                            PRODUCT_CD = code,
                            STATUS = "EXISTS",
                            MESSAGE = "Đã liên kết thành phẩm với đối tượng tập hợp chi phí có sẵn."
                        });
                        continue;
                    }

                    var created = await _departments.CreateAsync(companyCd, userId, new DepartmentInfoRequest
                    {
                        DEPARTMENT_CD = code,
                        PARENT_CD = string.Empty,
                        DEP_NAME_VIET = product.PRODUCT_NM_VIET,
                        DEP_NAME_ENG = product.PRODUCT_NM_ENG,
                        DEP_NAME_KOR = product.PRODUCT_NM_KOR,
                        DEP_NAME_CHINA = product.PRODUCT_NM_CHINA,
                        ISDEL = "0"
                    });

                    if (created.DEPARTMENT_ID <= 0 ||
                        !string.Equals(created.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Đối tượng mới tạo không hợp lệ hoặc sai mã thành phẩm.");

                    linkAttempted = true;
                    await _products.LinkDepartmentAsync(
                        companyCd, userId, productId, code, created.DEPARTMENT_ID, prefix);

                    result.CREATED++;
                    result.ITEMS.Add(new CostObjectSyncItemDto
                    {
                        PRODUCT_ID = productId,
                        PRODUCT_CD = code,
                        STATUS = "CREATED",
                        MESSAGE = "Đã tạo đối tượng và cập nhật DEPARTMENT_ID cho thành phẩm."
                    });
                }
                catch (Exception ex)
                {
                    // A concurrent sync might have created this code first.
                    // Recover only from a creation failure; never hide a failed
                    // product_info link behind a misleading EXISTS result.
                    if (!linkAttempted)
                    {
                        try
                        {
                            var concurrent = (await _departments.GetListAsync(companyCd, departmentCd: code))
                                .FirstOrDefault(d => d.ISDEL != "1" &&
                                    string.Equals(d.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase));

                            if (concurrent != null && concurrent.DEPARTMENT_ID > 0)
                            {
                                linkAttempted = true;
                                await _products.LinkDepartmentAsync(
                                    companyCd, userId, productId, code, concurrent.DEPARTMENT_ID, prefix);

                                result.EXISTS++;
                                result.ITEMS.Add(new CostObjectSyncItemDto
                                {
                                    PRODUCT_ID = productId,
                                    PRODUCT_CD = code,
                                    STATUS = "EXISTS",
                                    MESSAGE = "Đã liên kết thành phẩm với đối tượng cùng mã đã tồn tại."
                                });
                                continue;
                            }
                        }
                        catch (Exception recoveryEx)
                        {
                            ex = recoveryEx;
                        }
                    }

                    _logger.LogError(ex,
                        "Cost object sync/link failed. Company={Company} ProductId={ProductId} Code={Code}",
                        companyCd, productId, code);
                    result.FAILED++;
                    result.ITEMS.Add(new CostObjectSyncItemDto
                    {
                        PRODUCT_ID = productId,
                        PRODUCT_CD = code,
                        STATUS = "FAILED",
                        MESSAGE = "Không thể tạo hoặc liên kết đối tượng; kiểm tra danh mục và DEPARTMENT_ID."
                    });
                }
            }

            return Success(result);
        }

        // Menu code comes from the menu configured for this route, not from unrelated master modules.
        // Missing permission is an authorization failure (403), not an expired login (401).
        private async Task<bool> HasCostObjectPermissionAsync(string companyCd, string action)
        {
            var routeMenuCodes = (await _menus.GetMenuByCompanyAsync(companyCd))
                .Where(menu => !menu.IS_DISABLED
                    && string.Equals(menu.ROUTE_PATH?.Trim().TrimEnd('/'), CostObjectRoutePath, StringComparison.OrdinalIgnoreCase))
                .Select(menu => menu.MENU_CODE?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var menuCode in routeMenuCodes)
            {
                if (await Common.HasPermissionAsync(menuCode!, action))
                    return true;
            }

            _logger.LogWarning("Permission denied for cost object action {Action}, Company {CompanyCd}", action, companyCd);
            return false;
        }

        private static bool IsFinishedGood(ProductInfoDto product, string prefix)
            => product.PRODUCT_ID > 0
                && product.ISDEL != "1"
                && !string.IsNullOrWhiteSpace(product.PRODUCT_CD)
                && !string.IsNullOrWhiteSpace(product.DIVISION)
                && product.DIVISION.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static string? NormalizeDivisionPrefix(string? value)
        {
            var prefix = string.IsNullOrWhiteSpace(value)
                ? DefaultFinishedGoodsAccountPrefix
                : value.Trim();
            return prefix.Length <= 20 && prefix.All(char.IsDigit) ? prefix : null;
        }
    }
}
