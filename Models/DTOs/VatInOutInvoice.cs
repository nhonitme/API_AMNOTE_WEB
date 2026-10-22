namespace API_AMNOTE_WEB.Models.DTOs;

public sealed class VatInOutQuery
{
    public string TYPE { get; set; } = "BUY";
    public string? FROM_YMD { get; set; }
    public string? TO_YMD { get; set; }
    public string? STATUS { get; set; }
    public string? SEARCH_TEXT { get; set; }
    public string? LANGUAGE { get; set; }
}

// Property names intentionally match SQL aliases and the existing VAT popup fields.
public sealed class VatInOutInvoice
{
    public string COMPANY_CD { get; set; } = string.Empty;
    public string mhdon { get; set; } = string.Empty;
    public string? tthai { get; set; }
    public string? tthai_ten { get; set; }
    public string? khmshdon { get; set; }
    public string? khmshdon_ten { get; set; }
    public string? khhdon { get; set; }
    public string? tdlap { get; set; }
    public string? tdlap_ten { get; set; }
    public string? shdon { get; set; }
    public string? CHIT_NO { get; set; }
    public string? dvtte { get; set; }
    public decimal? TGia { get; set; }
    public string? mtdtchieu { get; set; }
    public string? ten { get; set; }
    public string? dchi { get; set; }
    public string? mst { get; set; }
    public string? nbten { get; set; }
    public string? nbdchi { get; set; }
    public string? nbmst { get; set; }
    public string? nmten { get; set; }
    public string? nmdchi { get; set; }
    public string? nmmst { get; set; }
    public decimal? tgtcthue { get; set; }
    public decimal? tgtthue { get; set; }
    public decimal? tgtttbso { get; set; }
    public decimal? ttcktmai { get; set; }
    public string? GChu { get; set; }
    public string? AUTO_CHIT_CD { get; set; }
    public string? CHIT_YMD { get; set; }
    public string? CHIT_TYPE { get; set; }
    public string? __CHIT_TYPE { get; set; }
    public long __CHIT_ID { get; set; }
}
