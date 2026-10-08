using System;

namespace API_AMNOTE_WEB.Models
{
    public class SysUserPermission
    {
        public long USER_PERMISSION_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USERID { get; set; } = string.Empty;
        public string MENU_CODE { get; set; } = string.Empty;
        public string CAN_VIEW { get; set; } = "0";
        public string CAN_ADD { get; set; } = "0";
        public string CAN_EDIT { get; set; } = "0";
        public string CAN_DELETE { get; set; } = "0";
        public string CAN_PRINT { get; set; } = "0";
        public string CAN_EXPORT { get; set; } = "0";
        public string CAN_IMPORT { get; set; } = "0";
        public string CAN_APPROVE { get; set; } = "0";
        public string USERID_INPUT { get; set; } = string.Empty;
        public DateTime? INPUT_YMD { get; set; }
        public string USERID_MODIFY { get; set; } = string.Empty;
        public DateTime? MODIFY_YMD { get; set; }
        public string ISDEL { get; set; } = "0";
        public string PERMISSION_SOURCE { get; set; } = string.Empty;

        public bool HasPermission(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
                return false;

            return permissionKey.Trim().ToUpperInvariant() switch
            {
                "VIEW" => CAN_VIEW == "1",
                "ADD" => CAN_ADD == "1",
                "EDIT" => CAN_EDIT == "1",
                "DELETE" => CAN_DELETE == "1",
                "PRINT" => CAN_PRINT == "1",
                "EXPORT" => CAN_EXPORT == "1",
                "IMPORT" => CAN_IMPORT == "1",
                "APPROVE" => CAN_APPROVE == "1",
                _ => false,
            };
        }
    }
}
