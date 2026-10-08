using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MenuController : BaseApiController
    {
        private readonly IMenuRepository _menuRepo;
        private readonly ISystemService _systemService;
        private readonly ILogger<MenuController> _logger;

        public MenuController(IMenuRepository menuRepo, ISystemService systemService, ILogger<MenuController> logger)
        {
            _menuRepo = menuRepo;
            _systemService = systemService;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetMenuList()
        {
            var companyCd = Common.GetCompanyCode();
            var menus = await _menuRepo.GetMenuByCompanyAsync(companyCd);
            foreach (var menu in menus)
            {
                NormalizeMenuCaption(menu);
            }

            return Success(menus, "Menu list loaded successfully");
        }

        [HttpGet("tree")]
        public async Task<IActionResult> GetMenuTree()
        {
            var companyCd = string.Empty;
            var userId = string.Empty;

            try
            {
                companyCd = Common.GetCompanyCode();
                userId = Common.GetUserId();
                _logger.LogInformation("Menu tree loading. CompanyCd: {CompanyCd}, UserId: {UserId}", companyCd, userId);

                var menus = (await _menuRepo.GetMenuByCompanyAsync(companyCd)).ToList();
                var visibleMenus = await FilterMenuByViewPermissionAsync(menus, companyCd, userId);
                var menuTree = BuildMenuTree(visibleMenus);

                _logger.LogInformation(
                    "Menu tree loaded. CompanyCd: {CompanyCd}, UserId: {UserId}, MenuCount: {MenuCount}, VisibleCount: {VisibleCount}, RootCount: {RootCount}",
                    companyCd,
                    userId,
                    menus.Count,
                    visibleMenus.Count,
                    menuTree.Count);

                return Success(menuTree, "Menu tree loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Menu tree load failed. CompanyCd: {CompanyCd}, UserId: {UserId}", companyCd, userId);
                throw;
            }
        }

        private async Task<List<Menu>> FilterMenuByViewPermissionAsync(List<Menu> menus, string companyCd, string userId)
        {
            if (menus.Count == 0)
            {
                return new List<Menu>();
            }

            var menuById = menus.ToDictionary(menu => menu.MENU_ID, menu => menu);
            var visibleMenuIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var permissions = (await _systemService.GetUserPermissionsAsync(companyCd, userId))
                .Where(permission => (permission.ISDEL ?? "0") == "0" && !string.IsNullOrWhiteSpace(permission.MENU_CODE))
                .GroupBy(permission => permission.MENU_CODE.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var menu in menus)
            {
                if (menu.IS_DISABLED || string.IsNullOrWhiteSpace(menu.MENU_CODE))
                {
                    continue;
                }

                if (permissions.TryGetValue(menu.MENU_CODE.Trim(), out var permission) && permission.HasPermission("VIEW"))
                {
                    visibleMenuIds.Add(menu.MENU_ID);
                }
            }

            foreach (var menuId in visibleMenuIds.ToArray())
            {
                var current = menuById[menuId];
                while (!string.IsNullOrWhiteSpace(current.PARENT_ID) && menuById.TryGetValue(current.PARENT_ID, out var parent))
                {
                    visibleMenuIds.Add(parent.MENU_ID);
                    current = parent;
                }
            }

            return menus.Where(menu => visibleMenuIds.Contains(menu.MENU_ID)).ToList();
        }

        private static void NormalizeMenuCaption(Menu menu)
        {
            menu.LABEL_TEXT = Common.NormalizeNullableText(menu.LABEL_TEXT);
            if (string.IsNullOrWhiteSpace(menu.CAPTION))
            {
                menu.CAPTION = menu.MENU_NAME;
            }
        }

        private List<MenuTreeNode> BuildMenuTree(IEnumerable<Menu> menus)
        {
            var allNodes = menus.Select(menu => new MenuTreeNode
            {
                MENU_ID = menu.MENU_ID,
                MENU_CODE = menu.MENU_CODE,
                MENU_NAME = menu.MENU_NAME,
                LABEL_TEXT = Common.NormalizeNullableText(menu.LABEL_TEXT),
                CAPTION = string.IsNullOrWhiteSpace(menu.CAPTION) ? menu.MENU_NAME : menu.CAPTION,
                PARENT_ID = menu.PARENT_ID,
                ROUTE_PATH = menu.ROUTE_PATH,
                ICON = menu.ICON,
                SORT_ORDER = menu.SORT_ORDER,
                IS_DISABLED = menu.IS_DISABLED,
                CHILDREN = new List<MenuTreeNode>()
            }).ToList();
            var rootNodes = allNodes.Where(node => node.PARENT_ID == null).ToList();

            foreach (var root in rootNodes)
            {
                BuildChildren(root, allNodes);
            }

            return rootNodes.OrderBy(node => node.SORT_ORDER).ToList();
        }

        private void BuildChildren(MenuTreeNode parent, List<MenuTreeNode> allNodes)
        {
            var children = allNodes
                .Where(node => node.PARENT_ID == parent.MENU_ID)
                .OrderBy(node => node.SORT_ORDER)
                .ToList();

            foreach (var child in children)
            {
                parent.CHILDREN.Add(child);
                BuildChildren(child, allNodes);
            }
        }
    }
}
