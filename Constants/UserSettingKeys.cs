namespace API_AMNOTE_WEB.Constants
{
    public static class UserSettingKeys
    {
        public const string DefaultCheckVat = "DEFAULT_CHECK_VAT";

        /// <summary>Bước khóa sổ đã chọn (1-3), VALUE phân cách dấu phẩy.</summary>
        public const string PeriodLockStepCodes = "PERIOD_LOCK_STEP_CODES";

        /// <summary>Bước 2 COGS_SUMMARY - cách chuyển giá vốn.</summary>
        public const string PeriodLockCogsRule = "PERIOD_LOCK_COGS_RULE";

        /// <summary>Bước 3 PROFIT_LOSS_REPORT - cách tính báo cáo lãi lỗ.</summary>
        public const string PeriodLockPlBalanceMethod = "PERIOD_LOCK_PL_BALANCE_METHOD";
    }
}
