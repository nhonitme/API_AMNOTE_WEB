namespace API_AMNOTE_WEB.Models
{
    public class DashboardKpiDto
    {
        public decimal CashBalance { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
        public decimal Profit { get; set; }
        public decimal Receivables { get; set; }
        public decimal Payables { get; set; }
        public decimal VatPayable { get; set; }
        public int PendingVouchers { get; set; }
        public decimal PrevRevenue { get; set; }
        public decimal PrevExpense { get; set; }
        public decimal PrevProfit { get; set; }
    }

    public class DashboardChartItemDto
    {
        public string Month { get; set; } = "";
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
        public decimal Profit { get; set; }
    }

    public class DashboardTaskItemDto
    {
        public string Category { get; set; } = "";
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public string Severity { get; set; } = "info";
        public string? ActionUrl { get; set; }
    }

    public class DashboardReceivableDto
    {
        public string CustomerCd { get; set; } = "";
        public string CustomerNm { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public string LastVoucherYmd { get; set; } = "";
    }

    public class DashboardPayableDto
    {
        public string VendorCd { get; set; } = "";
        public string VendorNm { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public string LastVoucherYmd { get; set; } = "";
    }

    public class DashboardTaxDeadlineDto
    {
        public string ReportNm { get; set; } = "";
        public string Period { get; set; } = "";
        public string DueDate { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class DashboardPeriodLockDto
    {
        public string CurrentPeriod { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string LockedBy { get; set; } = "";
        public string LockedAt { get; set; } = "";
        public List<DashboardPeriodLockStepDto> Steps { get; set; } = new();
    }

    public class DashboardPeriodLockStepDto
    {
        public string StepCode { get; set; } = "";
        public string StepName { get; set; } = "";
        public int StepOrder { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string StartedAt { get; set; } = "";
        public string FinishedAt { get; set; } = "";
    }

    public class DashboardRecentVoucherDto
    {
        public string ChitYmd { get; set; } = "";
        public string ChitNo { get; set; } = "";
        public string ChitCd { get; set; } = "";
        public string ChitType { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
