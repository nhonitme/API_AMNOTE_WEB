namespace API_AMNOTE_WEB.Models
{
    /// <summary>
    /// Menu model từ stored procedure sp_get_menu_by_company
    /// </summary>
    public class Menu
    {
        public string MENU_ID { get; set; } = string.Empty;
        public string MENU_CODE { get; set; } = string.Empty;
        public string MENU_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string? PARENT_ID { get; set; }
        public string? ROUTE_PATH { get; set; }
        public string? ICON { get; set; }
        public int SORT_ORDER { get; set; }
        public bool IS_ACTIVE { get; set; }
        public bool IS_VISIBLE { get; set; }
        public bool IS_DISABLED { get; set; }
    }

    /// <summary>
    /// Menu tree structure cho frontend
    /// </summary>
    public class MenuTreeNode
    {
        public string MENU_ID { get; set; } = string.Empty;
        public string MENU_CODE { get; set; } = string.Empty;
        public string MENU_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string? PARENT_ID { get; set; }
        public string? ROUTE_PATH { get; set; }
        public string? ICON { get; set; }
        public int SORT_ORDER { get; set; }
        public bool IS_DISABLED { get; set; }

        /// <summary>
        /// Children menus
        /// </summary>
        public List<MenuTreeNode> CHILDREN { get; set; } = new List<MenuTreeNode>();
    }
}