namespace API_AMNOTE_WEB.Models
{
    public class SysUserPermissionRequest
    {
        public string? COMPANY_CD { get; set; }
        public string? USERID { get; set; }
        public string? MENU_CODE { get; set; }

        public string? CAN_VIEW { get; set; }
        public string? CAN_ADD { get; set; }
        public string? CAN_EDIT { get; set; }
        public string? CAN_DELETE { get; set; }
        public string? CAN_PRINT { get; set; }
        public string? CAN_EXPORT { get; set; }
        public string? CAN_IMPORT { get; set; }
        public string? CAN_APPROVE { get; set; }

        public string? USERID_MODIFY { get; set; }
    }
}
