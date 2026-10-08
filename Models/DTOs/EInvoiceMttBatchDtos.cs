namespace API_AMNOTE_WEB.Models;

public class EInvoiceMttBatchRequest
{
    public long[] INVOICE_IDS { get; set; } = Array.Empty<long>();
}
public class EInvoiceMttBatchSignRequest
{
    public string XML { get; set; } = string.Empty;
}
public class EInvoiceMttBatchPayload
{
    public string BATCH_ID { get; set; } = string.Empty;
    public string RAW_XML { get; set; } = string.Empty;
    public DateTime NLAP { get; set; }
    public int COUNT { get; set; }
}
