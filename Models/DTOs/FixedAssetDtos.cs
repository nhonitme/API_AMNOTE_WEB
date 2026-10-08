namespace API_AMNOTE_WEB.Models.DTOs
{
    public class FixedAssetListItemDto
    {
        public long ASSET_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string ASSET_CD { get; set; } = string.Empty;
        public string ASSET_NM { get; set; } = string.Empty;
        public string ACC_CD { get; set; } = string.Empty;
        public string? ACC_NM_VIET { get; set; }
        public string? ACC_NM_ENG { get; set; }
        public string? ACC_NM_KOR { get; set; }
        public string? ACC_NM_CHINA { get; set; }
        public string? USE_DEPT_CD { get; set; }
        public string? RECEIVE_YMD { get; set; }
        public string USE_START_YMD { get; set; } = string.Empty;
        public string DEPRE_START_YM { get; set; } = string.Empty;
        public string DEPRE_END_YM { get; set; } = string.Empty;
        public decimal USEFUL_LIFE_MONTH { get; set; }
        public int NORMAL_MONTH_COUNT { get; set; }
        public decimal ORIGINAL_AMT { get; set; }
        public decimal ACCUM_DEPRE_AMT { get; set; }
        public decimal REMAIN_DEPRE_AMT { get; set; }
        public decimal FIRST_DEPRE_AMT { get; set; }
        public decimal NORMAL_DEPRE_AMT { get; set; }
        public decimal LAST_DEPRE_AMT { get; set; }
        public string? ACQ_CHIT_NO { get; set; }
        public string STATUS { get; set; } = "IN_USE";
        /// <summary>Display text for STATUS (export / PDF). Not persisted.</summary>
        public string? STATUS_TEXT { get; set; }
    }

    public class FixedAssetDto : FixedAssetListItemDto
    {
        public long? ACQ_CHITINFO_ID { get; set; }
        public long? ACQ_CHITDETAIL_ID { get; set; }
        public string? NOTE { get; set; }
    }

    public class FixedAssetAllocationDto
    {
        public long? ALLOC_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long ASSET_ID { get; set; }
        public int ALLOC_SEQ { get; set; } = 1;
        public string ALLOC_TYPE { get; set; } = "PERCENT";
        public decimal? ALLOC_RATE { get; set; }
        public decimal? FIRST_ALLOC_AMT { get; set; }
        public decimal? NORMAL_ALLOC_AMT { get; set; }
        public decimal? LAST_ALLOC_AMT { get; set; }
        public string DEBIT_ACCT_CD { get; set; } = string.Empty;
        public string CREDIT_ACCT_CD { get; set; } = "214";
        public string? DEBIT_ACCT_NM_VIET { get; set; }
        public string? DEBIT_ACCT_NM_ENG { get; set; }
        public string? DEBIT_ACCT_NM_KOR { get; set; }
        public string? DEBIT_ACCT_NM_CHINA { get; set; }
        public string? CREDIT_ACCT_NM_VIET { get; set; }
        public string? CREDIT_ACCT_NM_ENG { get; set; }
        public string? CREDIT_ACCT_NM_KOR { get; set; }
        public string? CREDIT_ACCT_NM_CHINA { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public string? DEP_NAME_VIET { get; set; }
        public string? DEP_NAME_ENG { get; set; }
        public string? DEP_NAME_KOR { get; set; }
        public string? DEP_NAME_CHINA { get; set; }
        public string? NOTE { get; set; }
    }

    public class FixedAssetSaveRequest
    {
        public FixedAssetDto ASSET { get; set; } = new();
        public List<FixedAssetAllocationDto> ALLOCATIONS { get; set; } = new();
    }

    public class FixedAssetDetailResponse
    {
        public FixedAssetDto ASSET { get; set; } = new();
        public List<FixedAssetAllocationDto> ALLOCATIONS { get; set; } = new();
    }

    public class FixedAssetDepreciationPreviewRequest
    {
        public string USE_START_YMD { get; set; } = string.Empty;
        public decimal USEFUL_LIFE_MONTH { get; set; }
        public decimal ORIGINAL_AMT { get; set; }
        public decimal ACCUM_DEPRE_AMT { get; set; }
    }

    public class FixedAssetDepreciationPreviewResponse
    {
        public string DEPRE_START_YM { get; set; } = string.Empty;
        public string DEPRE_END_YM { get; set; } = string.Empty;
        public decimal USEFUL_LIFE_MONTH { get; set; }
        public int NORMAL_MONTH_COUNT { get; set; }
        public decimal REMAIN_DEPRE_AMT { get; set; }
        public decimal FIRST_DEPRE_AMT { get; set; }
        public decimal NORMAL_DEPRE_AMT { get; set; }
        public decimal LAST_DEPRE_AMT { get; set; }
    }
}
