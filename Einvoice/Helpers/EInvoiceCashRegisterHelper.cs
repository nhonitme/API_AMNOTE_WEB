namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    internal static class EInvoiceCashRegisterHelper
    {
        public static bool IsCashRegister(string? series)
        {
            var normalized = series?.Trim();
            return normalized?.Length >= 4 && char.ToUpperInvariant(normalized[3]) == 'M';
        }

        // QD 1233: MTT signs TDiep/DLieu, not the ordinary invoice-signing flow.
        public static void EnsureOrdinarySigningAllowed(string? series)
        {
            if (IsCashRegister(series))
                throw new InvalidOperationException(
                    "Hóa đơn MTT cần luồng cấp mã và ký gói dữ liệu riêng (206/216). " +
                    "Hãy chọn hóa đơn và ký gói XML tại menu Hóa đơn máy tính tiền.");
        }
    }
}
