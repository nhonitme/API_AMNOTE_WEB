using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceMinuteDto
    {
        public long BBAN_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long? SELLER_ID { get; set; }
        public long? INVOICE_ID { get; set; }
        public long? REF_INVOICE_ID { get; set; }
        public string PBAN { get; set; } = "2.1.0";
        public string TBBAN { get; set; } = "Biên bản điều chỉnh hóa đơn";
        public string SBBAN { get; set; } = string.Empty;
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NBBAN { get; set; }
        public int TCHDON { get; set; } = 2;
        public string NBAN { get; set; } = string.Empty;
        public string MSTNBAN { get; set; } = string.Empty;
        public string? DCNBAN { get; set; }
        public string NMUA { get; set; } = string.Empty;
        public string? MSTNMUA { get; set; }
        public string? DCNMUA { get; set; }
        public string KHMSHDON { get; set; } = string.Empty;
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLAP { get; set; }
        public string DVTTE { get; set; } = "VND";
        public decimal? TGIA { get; set; } = 1m;
        public string? MTRACUU { get; set; }
        public string? TTKHAC_XML { get; set; }
        public string? NDBBAN_XML { get; set; }
        public string? SIGNED_XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int NMUA_IS_SIGNED { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NMUA_SIGN_DT { get; set; }
        public int IS_MAIL { get; set; }
        public string? CHECKSUM { get; set; }
        public int ISDEL { get; set; }
        public List<EInvoiceMinuteReasonDto> REASONS { get; set; } = new();
        public List<EInvoiceMinuteLineDto> LINES_BEFORE { get; set; } = new();
        public List<EInvoiceMinuteLineDto> LINES_AFTER { get; set; } = new();
        public EInvoiceMinuteLineDto? TOTAL_BEFORE { get; set; }
        public EInvoiceMinuteLineDto? TOTAL_AFTER { get; set; }
        public decimal? TOTAL_AFTER_TTHUE { get; set; }
    }

    public class EInvoiceMinuteReasonDto
    {
        public long REASON_ID { get; set; }
        public long BBAN_ID { get; set; }
        public int SORT_ORDER { get; set; } = 1;
        public string LDO { get; set; } = string.Empty;
        public int ISDEL { get; set; }
    }

    public class EInvoiceMinuteLineDto
    {
        public long REASON_ID { get; set; }
        public long BBAN_ID { get; set; }
        public long? DETAIL_ID { get; set; }
        public int LINE_SIDE { get; set; }
        public int SORT_ORDER { get; set; } = 1;
        public string LDO { get; set; } = string.Empty;
        public int? TCHAT { get; set; }
        public int? STT { get; set; }
        public string? MHHDVU { get; set; }
        public string? THHDVU { get; set; }
        public string? DVTINH { get; set; }
        public decimal? SLUONG { get; set; }
        public decimal? DGIA { get; set; }
        public decimal? TLCKHAU { get; set; }
        public decimal? STCKHAU { get; set; }
        public decimal? THTIEN { get; set; }
        public string? TSUAT { get; set; }
        public decimal? TTHUE { get; set; }
        public decimal? TSAUTHUE { get; set; }
        public string? EXTRA_JSON { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoiceMinuteSaveRequest : EInvoiceMinuteDto
    {
    }

    public class EInvoiceMinuteSearchRequest
    {
        public long? BbanId { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? FromDate { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? ToDate { get; set; }
        public string? Keyword { get; set; }
        public int? IsSigned { get; set; }
        public bool IncludeReasons { get; set; }
    }

    public class EInvoiceMinuteSigningPayloadDto
    {
        public long BBAN_ID { get; set; }
        public string RAW_XML { get; set; } = string.Empty;
        public bool IS_SIGNED { get; set; }
    }

    public class EInvoiceMinuteSignRequest
    {
        public string? XML { get; set; }
        public string? CERTIFICATE_SUBJECT { get; set; }
        public string? CERTIFICATE_THUMBPRINT { get; set; }
        public string? CERTIFICATE_SERIAL_NUMBER { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? SIGNED_AT { get; set; }
    }

    public class EInvoiceMinutePublicLookupDto
    {
        public long BBAN_ID { get; set; }
        public string? MTRACUU { get; set; }
        public string? TBBAN { get; set; }
        public string? SBBAN { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NBBAN { get; set; }
        public int TCHDON { get; set; }
        public string? NBAN { get; set; }
        public string? MSTNBAN { get; set; }
        public string? DCNBAN { get; set; }
        public string? NMUA { get; set; }
        public string? MSTNMUA { get; set; }
        public string? DCNMUA { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLAP { get; set; }
        public int IS_SIGNED { get; set; }
        public int NMUA_IS_SIGNED { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NMUA_SIGN_DT { get; set; }
        public bool CAN_SIGN { get; set; }
        public bool CAN_DOWNLOAD_PDF { get; set; } = true;
        public bool CAN_DOWNLOAD_XML { get; set; } = true;
        public bool CAN_DOWNLOAD_XSL { get; set; } = true;
        public bool CAN_DOWNLOAD_HTML { get; set; } = true;
        public string? XML { get; set; }
        public string? XSL { get; set; }
        public string? HTML { get; set; }
        public List<EInvoiceMinuteReasonDto> REASONS { get; set; } = new();
        public List<EInvoiceMinuteLineDto> LINES_BEFORE { get; set; } = new();
        public List<EInvoiceMinuteLineDto> LINES_AFTER { get; set; } = new();
        public EInvoiceMinuteLineDto? TOTAL_BEFORE { get; set; }
        public EInvoiceMinuteLineDto? TOTAL_AFTER { get; set; }
    }

    public class EInvoiceMinutePublicDownloadInfo
    {
        public long BBAN_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? DB_NAME { get; set; }
        public string? MTRACUU { get; set; }
        public string? SBBAN { get; set; }
    }

    public class EInvoiceMinuteDeleteManyRequest
    {
        public List<long> BbanIds { get; set; } = new();
    }

    public static class EInvoiceMinuteMapper
    {
        public const int LineSideReason = 0;
        public const int LineSideBefore = 1;
        public const int LineSideAfter = 2;
        public const int LineSideBeforeTotal = 3;
        public const int LineSideAfterTotal = 4;

        public static EInvoiceMinuteDto ToDto(EInvoiceMinuteInfo minute)
        {
            return new EInvoiceMinuteDto
            {
                BBAN_ID = minute.BBAN_ID,
                COMPANY_CD = minute.COMPANY_CD,
                SELLER_ID = minute.SELLER_ID,
                INVOICE_ID = minute.INVOICE_ID,
                REF_INVOICE_ID = minute.REF_INVOICE_ID,
                PBAN = minute.PBAN,
                TBBAN = minute.TBBAN,
                SBBAN = minute.SBBAN,
                NBBAN = minute.NBBAN,
                TCHDON = minute.TCHDON,
                NBAN = minute.NBAN,
                MSTNBAN = minute.MSTNBAN,
                DCNBAN = minute.DCNBAN,
                NMUA = minute.NMUA,
                MSTNMUA = minute.MSTNMUA,
                DCNMUA = minute.DCNMUA,
                KHMSHDON = minute.KHMSHDON,
                KHHDON = minute.KHHDON,
                SHDON = minute.SHDON,
                NLAP = minute.NLAP,
                DVTTE = minute.DVTTE,
                TGIA = minute.TGIA,
                MTRACUU = minute.MTRACUU,
                TTKHAC_XML = minute.TTKHAC_XML,
                NDBBAN_XML = minute.NDBBAN_XML,
                SIGNED_XML = minute.SIGNED_XML,
                IS_SIGNED = minute.IS_SIGNED,
                NMUA_IS_SIGNED = minute.NMUA_IS_SIGNED,
                NMUA_SIGN_DT = minute.NMUA_SIGN_DT,
                IS_MAIL = minute.IS_MAIL,
                CHECKSUM = minute.CHECKSUM,
                ISDEL = minute.ISDEL,
                REASONS = minute.REASONS.Select(ToReasonDto).ToList(),
                LINES_BEFORE = minute.LINES_BEFORE.Select(ToLineDto).ToList(),
                LINES_AFTER = minute.LINES_AFTER.Select(ToLineDto).ToList(),
                TOTAL_BEFORE = minute.TOTAL_BEFORE == null ? null : ToLineDto(minute.TOTAL_BEFORE),
                TOTAL_AFTER = minute.TOTAL_AFTER == null ? null : ToLineDto(minute.TOTAL_AFTER),
                TOTAL_AFTER_TTHUE = minute.TOTAL_AFTER?.TTHUE ?? minute.TOTAL_AFTER_TTHUE
            };
        }

        public static EInvoiceMinuteInfo ToEntity(EInvoiceMinuteSaveRequest request, string companyCd)
        {
            return new EInvoiceMinuteInfo
            {
                BBAN_ID = request.BBAN_ID,
                COMPANY_CD = companyCd,
                SELLER_ID = request.SELLER_ID,
                INVOICE_ID = request.INVOICE_ID,
                REF_INVOICE_ID = request.REF_INVOICE_ID,
                PBAN = request.PBAN,
                TBBAN = request.TBBAN,
                SBBAN = request.SBBAN,
                NBBAN = request.NBBAN,
                TCHDON = request.TCHDON,
                NBAN = request.NBAN,
                MSTNBAN = request.MSTNBAN,
                DCNBAN = request.DCNBAN,
                NMUA = request.NMUA,
                MSTNMUA = request.MSTNMUA,
                DCNMUA = request.DCNMUA,
                KHMSHDON = request.KHMSHDON,
                KHHDON = request.KHHDON,
                SHDON = request.SHDON,
                NLAP = request.NLAP,
                DVTTE = request.DVTTE,
                TGIA = request.TGIA,
                MTRACUU = request.MTRACUU,
                TTKHAC_XML = request.TTKHAC_XML,
                NDBBAN_XML = request.NDBBAN_XML,
                SIGNED_XML = request.SIGNED_XML,
                IS_SIGNED = request.IS_SIGNED,
                NMUA_IS_SIGNED = request.NMUA_IS_SIGNED,
                NMUA_SIGN_DT = request.NMUA_SIGN_DT,
                IS_MAIL = request.IS_MAIL,
                CHECKSUM = request.CHECKSUM,
                ISDEL = request.ISDEL,
                REASONS = request.REASONS.Select(ToReasonEntity).ToList(),
                LINES_BEFORE = request.LINES_BEFORE.Select(line => ToLineEntity(line, LineSideBefore)).ToList(),
                LINES_AFTER = request.LINES_AFTER.Select(line => ToLineEntity(line, LineSideAfter)).ToList(),
                TOTAL_BEFORE = request.TOTAL_BEFORE == null ? null : ToLineEntity(request.TOTAL_BEFORE, LineSideBeforeTotal),
                TOTAL_AFTER = request.TOTAL_AFTER == null ? null : ToLineEntity(request.TOTAL_AFTER, LineSideAfterTotal)
            };
        }

        public static void SplitStoredRows(IEnumerable<EInvoiceMinuteLine> rows, EInvoiceMinuteInfo minute)
        {
            minute.REASONS.Clear();
            minute.LINES_BEFORE.Clear();
            minute.LINES_AFTER.Clear();
            minute.TOTAL_BEFORE = null;
            minute.TOTAL_AFTER = null;

            foreach (var row in rows.Where(x => x.ISDEL != 1))
            {
                switch (row.LINE_SIDE)
                {
                    case LineSideBefore:
                        minute.LINES_BEFORE.Add(row);
                        break;
                    case LineSideAfter:
                        minute.LINES_AFTER.Add(row);
                        break;
                    case LineSideBeforeTotal:
                        minute.TOTAL_BEFORE = row;
                        break;
                    case LineSideAfterTotal:
                        minute.TOTAL_AFTER = row;
                        break;
                    default:
                        minute.REASONS.Add(new EInvoiceMinuteReason
                        {
                            REASON_ID = row.REASON_ID,
                            BBAN_ID = row.BBAN_ID,
                            SORT_ORDER = row.SORT_ORDER,
                            LDO = row.LDO,
                            ISDEL = row.ISDEL
                        });
                        break;
                }
            }
        }

        public static EInvoiceMinuteLine ToStoredLine(EInvoiceMinuteReason reason)
        {
            return new EInvoiceMinuteLine
            {
                REASON_ID = reason.REASON_ID,
                BBAN_ID = reason.BBAN_ID,
                LINE_SIDE = LineSideReason,
                SORT_ORDER = reason.SORT_ORDER,
                LDO = reason.LDO,
                ISDEL = reason.ISDEL
            };
        }

        public static EInvoiceMinuteLine ToStoredLine(EInvoiceMinuteLine line, int lineSide)
        {
            line.LINE_SIDE = lineSide;
            return line;
        }

        public static List<EInvoiceMinuteLine> BuildPersistRows(EInvoiceMinuteInfo minute)
        {
            var rows = new List<EInvoiceMinuteLine>();

            var reasonIndex = 0;
            foreach (var reason in minute.REASONS.Where(x => x.ISDEL != 1))
            {
                reasonIndex += 1;
                var row = ToStoredLine(reason);
                row.SORT_ORDER = reasonIndex;
                rows.Add(row);
            }

            var beforeIndex = 0;
            foreach (var line in minute.LINES_BEFORE.Where(x => x.ISDEL != 1))
            {
                beforeIndex += 1;
                var row = ToStoredLine(line, LineSideBefore);
                row.SORT_ORDER = beforeIndex;
                rows.Add(row);
            }

            var afterIndex = 0;
            foreach (var line in minute.LINES_AFTER.Where(x => x.ISDEL != 1))
            {
                afterIndex += 1;
                var row = ToStoredLine(line, LineSideAfter);
                row.SORT_ORDER = afterIndex;
                rows.Add(row);
            }

            if (minute.TOTAL_BEFORE is { ISDEL: not 1 })
            {
                var row = ToStoredLine(minute.TOTAL_BEFORE, LineSideBeforeTotal);
                row.SORT_ORDER = 9999;
                rows.Add(row);
            }

            if (minute.TOTAL_AFTER is { ISDEL: not 1 })
            {
                var row = ToStoredLine(minute.TOTAL_AFTER, LineSideAfterTotal);
                row.SORT_ORDER = 9999;
                rows.Add(row);
            }

            return rows;
        }

        private static EInvoiceMinuteReasonDto ToReasonDto(EInvoiceMinuteReason reason)
        {
            return new EInvoiceMinuteReasonDto
            {
                REASON_ID = reason.REASON_ID,
                BBAN_ID = reason.BBAN_ID,
                SORT_ORDER = reason.SORT_ORDER,
                LDO = reason.LDO,
                ISDEL = reason.ISDEL
            };
        }

        private static EInvoiceMinuteLineDto ToLineDto(EInvoiceMinuteLine line)
        {
            return new EInvoiceMinuteLineDto
            {
                REASON_ID = line.REASON_ID,
                BBAN_ID = line.BBAN_ID,
                DETAIL_ID = line.DETAIL_ID,
                LINE_SIDE = line.LINE_SIDE,
                SORT_ORDER = line.SORT_ORDER,
                LDO = line.LDO,
                TCHAT = line.TCHAT,
                STT = line.STT,
                MHHDVU = line.MHHDVU,
                THHDVU = line.THHDVU,
                DVTINH = line.DVTINH,
                SLUONG = line.SLUONG,
                DGIA = line.DGIA,
                TLCKHAU = line.TLCKHAU,
                STCKHAU = line.STCKHAU,
                THTIEN = line.THTIEN,
                TSUAT = line.TSUAT,
                TTHUE = line.TTHUE,
                TSAUTHUE = line.TSAUTHUE,
                EXTRA_JSON = line.EXTRA_JSON,
                ISDEL = line.ISDEL
            };
        }

        private static EInvoiceMinuteReason ToReasonEntity(EInvoiceMinuteReasonDto reason)
        {
            return new EInvoiceMinuteReason
            {
                REASON_ID = reason.REASON_ID,
                BBAN_ID = reason.BBAN_ID,
                SORT_ORDER = reason.SORT_ORDER,
                LDO = reason.LDO,
                ISDEL = reason.ISDEL
            };
        }

        private static EInvoiceMinuteLine ToLineEntity(EInvoiceMinuteLineDto line, int lineSide)
        {
            return new EInvoiceMinuteLine
            {
                REASON_ID = line.REASON_ID,
                BBAN_ID = line.BBAN_ID,
                DETAIL_ID = line.DETAIL_ID,
                LINE_SIDE = lineSide,
                SORT_ORDER = line.SORT_ORDER,
                LDO = line.LDO,
                TCHAT = line.TCHAT,
                STT = line.STT,
                MHHDVU = line.MHHDVU,
                THHDVU = line.THHDVU,
                DVTINH = line.DVTINH,
                SLUONG = line.SLUONG,
                DGIA = line.DGIA,
                TLCKHAU = line.TLCKHAU,
                STCKHAU = line.STCKHAU,
                THTIEN = line.THTIEN,
                TSUAT = line.TSUAT,
                TTHUE = line.TTHUE,
                TSAUTHUE = line.TSAUTHUE,
                EXTRA_JSON = line.EXTRA_JSON,
                ISDEL = line.ISDEL
            };
        }
    }
}
