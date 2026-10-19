namespace API_AMNOTE_WEB.Models
{
    // Column names mirror product_material_info and its display joins.
    public class ProductMaterialInfoDto
    {
        public long MATERIAL_ID { get; set; }
        public string COMPANY_CD { get; set; } = "";
        public long PRODUCT_ID { get; set; }
        public long MATERIAL_PRODUCT_ID { get; set; }
        public decimal QUANTITY { get; set; }
        public string? MATERIAL_PRODUCT_CD { get; set; }
        public string? MATERIAL_PRODUCT_NM { get; set; }
        public string? UNIT_NM { get; set; }
    }

    public class ProductMaterialCountDto
    {
        public long PRODUCT_ID { get; set; }
        public int MATERIAL_COUNT { get; set; }
    }

    public class ProductMaterialSaveRequest
    {
        public long PRODUCT_ID { get; set; }
        public long MATERIAL_PRODUCT_ID { get; set; }
        public decimal QUANTITY { get; set; }
    }
}
