namespace API_AMNOTE_WEB.Models;

public sealed class DebtOffsetVoucher
{
    public long CUSTOMER_ID { get; set; }
    public string? CUSTOMER_CD { get; set; }
    public string? CUSTOMER_NM_VIET { get; set; }
    public string? CUSTOMER_NM_ENG { get; set; }
    public long PAY_ID { get; set; }
    public long CHIT_ID { get; set; }
    public string? CHIT_TYPE { get; set; }
    public string? CHIT_NO { get; set; }
    public string? CHIT_YMD { get; set; }
    public string? VAT_CHIT_NO { get; set; }
    public string? VAT_YMD { get; set; }
    public string? DESCRIPTION_VIET { get; set; }
    public decimal AMOUNT { get; set; }
    public decimal FC_AMOUNT { get; set; }
    public decimal REMAINAMOUNT { get; set; }
    public decimal REMAINAMOUNT_FC { get; set; }
}

public sealed class DebtOffsetAvailable
{
    public IReadOnlyList<DebtOffsetVoucher> SOURCES { get; set; } = Array.Empty<DebtOffsetVoucher>();
    public IReadOnlyList<DebtOffsetVoucher> DEBTS { get; set; } = Array.Empty<DebtOffsetVoucher>();
}

public sealed class DebtOffsetHistory
{
    public long CUSTOMER_ID { get; set; }
    public string? CUSTOMER_CD { get; set; }
    public string? CUSTOMER_NM_VIET { get; set; }
    public long CHITPAYMENT_ID { get; set; }
    public long CHITCOLLECT_ID { get; set; }
    public long CHIT_ID_PAY { get; set; }
    public long CHIT_ID_DEBT { get; set; }
    public string? CHIT_NO_PAY { get; set; }
    public string? CHIT_NO_DEBT { get; set; }
    public string? CHIT_YMD_PAY { get; set; }
    public string? CHIT_YMD_DEBT { get; set; }
    public string? DESCRIPTION_VIET_PAY { get; set; }
    public string? DESCRIPTION_VIET_DEBT { get; set; }
    public decimal PAYAMOUNT { get; set; }
    public decimal PAYAMOUNT_FC { get; set; }
}

public sealed class DebtOffsetSaveRequest
{
    public string? Kind { get; set; }
    public long SourceChitId { get; set; }
    public long DebtChitId { get; set; }
    public long CustomerId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountFc { get; set; }
    public string? Currency { get; set; }
}

public sealed class DebtOffsetDeleteRequest
{
    public string? Kind { get; set; }
    public long RecordId { get; set; }
}

public sealed class DebtOffsetSaveResult
{
    public long CHITPAYMENT_ID { get; set; }
    public long CHITCOLLECT_ID { get; set; }
}

public sealed class DebtOffsetDeleteResult
{
    public int DELETED_COUNT { get; set; }
}
