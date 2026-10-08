namespace API_AMNOTE_WEB.Models.DTOs
{
    public class BankLookupDto
    {
        public long BANK_ID { get; set; }
        public string BANK_CD { get; set; } = string.Empty;
        public string BANK_NM { get; set; } = string.Empty;
        public string ACC_CD { get; set; } = string.Empty;
        public string PASSBOOK_NM { get; set; } = string.Empty;
        public string ACCOUNT_NUM { get; set; } = string.Empty;
        public string CITAD_CODE { get; set; } = string.Empty;
    }

    public class CustomerLookupDto
    {
        public long CUSTOMER_ID { get; set; }
        public string CUSTOMER_CD { get; set; } = string.Empty;
        public string CUSTOMER_NM_VIET { get; set; } = string.Empty;
        public string CUSTOMER_NM_ENG { get; set; } = string.Empty;
        public string CUSTOMER_NM_KOR { get; set; } = string.Empty;
        public string CUSTOMER_NM_CHINA { get; set; } = string.Empty;
        public string TAX_CD { get; set; } = string.Empty;
        public string ADDRESS { get; set; } = string.Empty;
    }

    public class DepartmentLookupDto
    {
        public long DEPARTMENT_ID { get; set; }
        public string DEPARTMENT_CD { get; set; } = string.Empty;
        public string DEP_NAME_VIET { get; set; } = string.Empty;
        public string DEP_NAME_ENG { get; set; } = string.Empty;
        public string DEP_NAME_KOR { get; set; } = string.Empty;
        public string DEP_NAME_CHINA { get; set; } = string.Empty;
    }

    public class ManagementLookupDto
    {
        public long MG_ID { get; set; }
        public string MG_CD { get; set; } = string.Empty;
        public string MG_DESC_VIET { get; set; } = string.Empty;
        public string MG_DESC_ENG { get; set; } = string.Empty;
        public string MG_DESC_KOR { get; set; } = string.Empty;
        public string MG_CD_ROOT { get; set; } = string.Empty;
    }

    public class StoreLookupDto
    {
        public int STORE_ID { get; set; }
        public string STORE_CD { get; set; } = string.Empty;
        public string STORE_NM_VIET { get; set; } = string.Empty;
        public string STORE_NM_ENG { get; set; } = string.Empty;
        public string STORE_NM_KOR { get; set; } = string.Empty;
        public int STORE_KIND_ID { get; set; }
        public string STORE_KIND_CD { get; set; } = string.Empty;
        public string STORE_KIND_NM_VIET { get; set; } = string.Empty;
    }

    public class StoreKindLookupDto
    {
        public int STORE_KIND_ID { get; set; }
        public string STORE_KIND_CD { get; set; } = string.Empty;
        public string STORE_KIND_NM_VIET { get; set; } = string.Empty;
        public string STORE_KIND_NM_ENG { get; set; } = string.Empty;
        public string STORE_KIND_NM_KOR { get; set; } = string.Empty;
    }

    public class ProductLookupDto
    {
        public int PRODUCT_ID { get; set; }
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string PRODUCT_NM_VIET { get; set; } = string.Empty;
        public string PRODUCT_NM_ENG { get; set; } = string.Empty;
        public string PRODUCT_NM_KOR { get; set; } = string.Empty;
        public string PRODUCT_NM_CHINA { get; set; } = string.Empty;
        public int PRODUCT_KIND_ID { get; set; }
        public string PRODUCT_KIND_CD { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_VIET { get; set; } = string.Empty;
        public int UNIT_ID { get; set; }
        public string UNIT_CD { get; set; } = string.Empty;
        public string UNIT_NM { get; set; } = string.Empty;
        public int STORE_ID { get; set; }
        public string STORE_CD { get; set; } = string.Empty;
        public string STORE_NM_VIET { get; set; } = string.Empty;
        public string DIVISION { get; set; } = string.Empty;
    }

    public class ProductKindLookupDto
    {
        public int PRODUCT_KIND_ID { get; set; }
        public string PRODUCT_KIND_CD { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_VIET { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_ENG { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_KOR { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_CHINA { get; set; } = string.Empty;
    }

    public class UnitLookupDto
    {
        public int UNIT_ID { get; set; }
        public string UNIT_CD { get; set; } = string.Empty;
        public string UNIT_NM { get; set; } = string.Empty;
    }

    public class CountryLookupDto
    {
        public int COUNTRY_ID { get; set; }
        public string COUNTRY_CD { get; set; } = string.Empty;
        public string COUNTRY_NM { get; set; } = string.Empty;
    }
}
