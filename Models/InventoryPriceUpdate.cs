namespace API_AMNOTE_WEB.Models;

public sealed class InventoryPriceUpdateRow
{
    public string PRODUCT_CD { get; set; } = string.Empty;
    public string PRODUCT_NAME { get; set; } = string.Empty;
    public long MOVEMENT_COUNT { get; set; }
    public decimal TOTAL_QUANTITY { get; set; }
    public decimal CURRENT_PRICE_MIN { get; set; }
    public decimal CURRENT_PRICE_MAX { get; set; }
    public decimal CURRENT_AMOUNT { get; set; }
    public decimal? NEW_UNIT_PRICE { get; set; }
}

public sealed class InventoryPriceUpdateRequest
{
    public string? STORE_CD { get; set; }
    public string? FROM_YMD { get; set; }
    public string? TO_YMD { get; set; }
    public List<InventoryPriceUpdateRow>? ROWS { get; set; }
}

public sealed class InventoryPriceUpdateResult
{
    public int UPDATED_PRODUCTS { get; init; }
}
