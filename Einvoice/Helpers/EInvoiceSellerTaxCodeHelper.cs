namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    /// <summary>MST hộ kinh doanh / CNKD: đúng 12 chữ số (thường là CCCD).</summary>
    internal static class EInvoiceSellerTaxCodeHelper
    {
        public static bool IsHouseholdBusinessTaxCode(string? taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
            {
                return false;
            }

            var digitCount = 0;
            foreach (var ch in taxCode)
            {
                if (char.IsDigit(ch))
                {
                    digitCount++;
                }
                else if (!char.IsWhiteSpace(ch) && ch is not '-' and not '.')
                {
                    return false;
                }
            }

            return digitCount == 12;
        }
    }
}
