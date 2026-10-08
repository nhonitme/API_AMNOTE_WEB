namespace API_AMNOTE_WEB.Models
{
    public sealed class CacheClearSummary
    {
        public int MasterData { get; init; }

        public int SysConfig { get; init; }

        public int UserSetting { get; init; }

        public int SysCodeSequence { get; init; }

        public int RemainingMemoryCache { get; init; }

        public int Total =>
            MasterData +
            SysConfig +
            UserSetting +
            SysCodeSequence +
            RemainingMemoryCache;
    }
}
