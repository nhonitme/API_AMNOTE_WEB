namespace API_AMNOTE_WEB.Models
{
    public class EInvoicePrintOptions
    {
        public bool IsConvertedPrint { get; init; }

        public string? ConvertedByNm { get; init; }

        public string? XmlFtpPath { get; init; }

        public long? SellerId { get; init; }

        public long? XslId { get; init; }

        public string? Khhdon { get; init; }

        public string? Shdon { get; init; }

        public string? Mtracuu { get; init; }

        public int? IsSigned { get; init; }
    }
}
