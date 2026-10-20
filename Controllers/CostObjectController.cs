using API_AMNOTE_WEB.Helpers;
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
        private const string InventoryMenuCode = "MD_INVENTORY";
        // DepartmentInfoController hiện đang bảo vệ thao tác bằng mã này.
        private const string DepartmentPermissionCode = "MD_COST_CENTER";
        private const string DefaultFinishedGoodsAccountPrefix = "155";

        private readonly IProductInfoService _products;
        private readonly IDepartmentInfoService _departments;
        private readonly ILogger<CostObjectController> _logger;

        public CostObjectController(
            IProductInfoService products,
            IDepartmentInfoService departments,
            ILogger<CostObjectController> logger)
        {
            _products = products;
            _departments = departments;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetOverview([FromQuery] string? divisionPrefix = null)
        {
            await EnsurePermissionAsync(InventoryMenuCode, "VIEW");
            await EnsurePermissionAsync(DepartmentPermissionCode, "VIEW");

            var prefix = NormalizeDivisionPrefix(divisionPrefix);
            if (prefix == null)
                return ValidationError("Mã tài khoản thành phẩm không hợp lệ (1-20 chữ số).");

            var companyCd = Common.GetCompanyCode();
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
                    DIVISION = p.DIVISION,
                    DEPARTMENT_ID = department?.DEPARTMENT_ID,
                    DEPARTMENT_CD = department?.DEPARTMENT_CD,
                    DEP_NAME_VIET = department?.DEP_NAME_VIET,
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
            await EnsurePermissionAsync(InventoryMenuCode, "VIEW");
            await EnsurePermissionAsync(DepartmentPermissionCode, "ADD");

            if (request?.ProductIds == null || request.ProductIds.Count == 0)
                return ValidationError("Vui lòng chọn ít nhất một thành phẩm.");

            var ids = request.ProductIds.Distinct().ToList();
            if (ids.Count > 200 || ids.Any(id => id <= 0))
                return ValidationError("Mỗi lần đồng bộ chọn từ 1 đến 200 thành phẩm hợp lệ.");

            var prefix = NormalizeDivisionPrefix(request.DivisionPrefix);
            if (prefix == null)
                return ValidationError("Mã tài khoản thành phẩm không hợp lệ (1-20 chữ số).");

            var companyCd = Common.GetCompanyCode();
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
                try
                {
                    // Tìm đối tượng theo COMPANY_CD và mã. Không gán tùy ý sang đối tượng khác.
                    var existing = (await _departments.GetListAsync(companyCd, departmentCd: code))
                        .FirstOrDefault(d => d.ISDEL != "1" &&
                            string.Equals(d.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        result.EXISTS++;
                        result.ITEMS.Add(new CostObjectSyncItemDto
                        {
                            PRODUCT_ID = productId,
                            PRODUCT_CD = code,
                            STATUS = "EXISTS",
                            MESSAGE = "Đối tượng cùng mã đã tồn tại, không ghi đè."
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

                    if (!string.Equals(created.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Mã đối tượng vừa tạo không khớp mã thành phẩm.");

                    result.CREATED++;
                    result.ITEMS.Add(new CostObjectSyncItemDto
                    {
                        PRODUCT_ID = productId,
                        PRODUCT_CD = code,
                        STATUS = "CREATED",
                        MESSAGE = "Đã tạo đối tượng tập hợp chi phí."
                    });
                }
                catch (Exception ex)
                {
                    // Khi chạy đồng thời, thao tác khác có thể đã tạo mã này.
                    var existing = (await _departments.GetListAsync(companyCd, departmentCd: code))
                        .Any(d => d.ISDEL != "1" &&
                            string.Equals(d.DEPARTMENT_CD?.Trim(), code, StringComparison.OrdinalIgnoreCase));
                    if (existing)
                    {
                        result.EXISTS++;
                        result.ITEMS.Add(new CostObjectSyncItemDto
                        {
                            PRODUCT_ID = productId,
                            PRODUCT_CD = code,
                            STATUS = "EXISTS",
                            MESSAGE = "Đối tượng cùng mã đã tồn tại."
                        });
                        continue;
                    }

                    _logger.LogError(ex, "Cost object sync failed. Company={Company} ProductId={ProductId}", companyCd, productId);
                    result.FAILED++;
                    result.ITEMS.Add(new CostObjectSyncItemDto
                    {
                        PRODUCT_ID = productId,
                        PRODUCT_CD = code,
                        STATUS = "FAILED",
                        MESSAGE = "Không thể tạo đối tượng; kiểm tra mã và cấu hình danh mục."
                    });
                }
            }

            return Success(result);
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
