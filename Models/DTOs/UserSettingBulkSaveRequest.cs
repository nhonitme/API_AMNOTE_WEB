namespace API_AMNOTE_WEB.Models.DTOs
{
    public class UserSettingBulkSaveRequest
    {
        public List<UserSettingSaveRequest> ITEMS { get; set; } = new();
    }
}
