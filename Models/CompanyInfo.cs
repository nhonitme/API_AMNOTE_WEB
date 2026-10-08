using System;

namespace API_AMNOTE_WEB.Models
{
    public class CompanyInfo
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? DB_GROUP_ID { get; set; }
        public string? COMPANY_NM { get; set; }
        public string? COMPANY_NM_EN { get; set; }
        public string? COMPANY_NM_KOR { get; set; }
        public short? COMPANY_TYPE { get; set; }
        public short? COMPANY_KIND { get; set; }
        public string? DE_COMPANY_CD { get; set; }
        public string? COMPANY_LV { get; set; }
        public string? ACCDATE_CD { get; set; }
        public string? TAX_CD { get; set; }
        public string? CCCDan { get; set; }
        public string? TCQTQLy { get; set; }
        public string? MCQTQLy { get; set; }
        public string? BRN { get; set; }
        public string? CRN { get; set; }
        public string? OWNER_NM { get; set; }
        public string? ZIP_CODE { get; set; }
        public string? ADDRESS_DO { get; set; }
        public string? ADDRESS { get; set; }
        public string? ADDRESS_ENG { get; set; }
        public string? ADDRESS_KOR { get; set; }
        public string? CARRYFORWARD_YMD { get; set; }
        public string? SIDO { get; set; }
        public string? GUMYUN { get; set; }
        public string? BUSINESS_TYPE { get; set; }
        public string? KIND_BUSINESS { get; set; }
        public string? TEL { get; set; }
        public string? EMAIL { get; set; }
        public string? WEBSITE { get; set; }
        public string? FAX { get; set; }
        public string? STOCKCALC_TYPE { get; set; }
        public string? OPEN_YMD { get; set; }
        public string? DECISION { get; set; }
        public DateTime? REG_YMD { get; set; }
        public string ISDEL { get; set; } = "0";
        public string? NOTE { get; set; }
    }

    public class CompanyInfoRequest
    {
        public string? COMPANY_CD { get; set; }
        public int? DB_GROUP_ID { get; set; }
        public string? COMPANY_NM { get; set; }
        public string? COMPANY_NM_EN { get; set; }
        public string? COMPANY_NM_KOR { get; set; }
        public short? COMPANY_TYPE { get; set; }
        public short? COMPANY_KIND { get; set; }
        public string? DE_COMPANY_CD { get; set; }
        public string? COMPANY_LV { get; set; }
        public string? ACCDATE_CD { get; set; }
        public string? TAX_CD { get; set; }
        public string? CCCDan { get; set; }
        public string? TCQTQLy { get; set; }
        public string? MCQTQLy { get; set; }
        public string? BRN { get; set; }
        public string? CRN { get; set; }
        public string? OWNER_NM { get; set; }
        public string? ZIP_CODE { get; set; }
        public string? ADDRESS_DO { get; set; }
        public string? ADDRESS { get; set; }
        public string? ADDRESS_ENG { get; set; }
        public string? ADDRESS_KOR { get; set; }
        public string? CARRYFORWARD_YMD { get; set; }
        public string? SIDO { get; set; }
        public string? GUMYUN { get; set; }
        public string? BUSINESS_TYPE { get; set; }
        public string? KIND_BUSINESS { get; set; }
        public string? TEL { get; set; }
        public string? EMAIL { get; set; }
        public string? WEBSITE { get; set; }
        public string? FAX { get; set; }
        public string? STOCKCALC_TYPE { get; set; }
        public string? OPEN_YMD { get; set; }
        public string? DECISION { get; set; }
        public DateTime? REG_YMD { get; set; }
        public string? ISDEL { get; set; }
        public string? NOTE { get; set; }
    }
}
