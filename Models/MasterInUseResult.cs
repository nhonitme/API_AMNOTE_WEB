namespace API_AMNOTE_WEB.Models
{
    public sealed class MasterInUseResult
    {
        public string IS_USED { get; set; } = "N";
        public string USED_TABLE { get; set; } = string.Empty;
        public string MESSAGE { get; set; } = string.Empty;

        public bool IsUsed => string.Equals(IS_USED, "Y", StringComparison.OrdinalIgnoreCase);
    }
}
