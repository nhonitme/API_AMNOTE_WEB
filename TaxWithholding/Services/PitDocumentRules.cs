namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitDocumentRules
{
    public const string IncomeTypeCodeType = "PIT_INCOME_TYPE";
    public static bool IsIncomeType(string value) => value.Length == 1 && value[0] is >= '1' and <= '8';
    // Appendix XIV explicitly includes CTT50.
    public static bool IsNoticeDocumentType(string value) => value is "1" or "2" or "3" or "4" or "CTT50" or "6" or "7" or "8";
    public static string Signer(string kind) => kind switch
    {
        PitKinds.Certificate => "TCTTNhap",
        PitKinds.Declaration or PitKinds.ErrorNotice => "NNT",
        _ => throw new ArgumentException("Loại chứng từ không hợp lệ.")
    };
}
