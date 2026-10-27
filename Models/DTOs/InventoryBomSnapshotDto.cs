using System.Collections.Generic;

namespace API_AMNOTE_WEB.Models.DTOs
{
    // CLIENT_KEY/OUTPUT_CLIENT_KEY are request-only correlation keys; not DB columns.
    // When loading a saved snapshot, CLIENT_KEY is reconstructed from persisted ID.
    public sealed class InventoryBomFinishedDto
    {
        public long? FINISHED_LINE_ID { get; set; }
        public string CLIENT_KEY { get; set; } = "";
        public long FINISHED_PRODUCT_ID { get; set; }
        public string? PRODUCT_CD { get; set; }
        public string? PRODUCT_NM_VIET { get; set; }
        public string? UNIT_CD { get; set; }
        public decimal FINISHED_QUANTITY { get; set; }
        public int SORT { get; set; }
    }

    public sealed class InventoryBomMaterialDto
    {
        public long? BOM_MATERIAL_ID { get; set; }
        public string CLIENT_KEY { get; set; } = "";
        public string FINISHED_CLIENT_KEY { get; set; } = "";
        public string OUTPUT_CLIENT_KEY { get; set; } = "";
        public long? FINISHED_LINE_ID { get; set; }
        public long MATERIAL_PRODUCT_ID { get; set; }
        public string? MATERIAL_PRODUCT_CD { get; set; }
        public string? MATERIAL_PRODUCT_NM { get; set; }
        public string? UNIT_CD { get; set; }
        public long? STORE_ID { get; set; }
        public decimal BOM_QUANTITY { get; set; }
        public decimal CALCULATED_QUANTITY { get; set; }
        public decimal CONFIRMED_QUANTITY { get; set; }
        public long? OUTPUT_ID { get; set; }
        public int SORT { get; set; }
    }

    public sealed class InventoryBomSnapshotDto
    {
        public List<InventoryBomFinishedDto> FINISHED { get; set; } = new();
        public List<InventoryBomMaterialDto> MATERIALS { get; set; } = new();
    }
}
