namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceErrorNoticeMapper
    {
        public static EInvoiceErrorNoticeDto ToDto(EInvoiceErrorNoticeInfo entity)
        {
            return new EInvoiceErrorNoticeDto
            {
                TBAO_ID = entity.TBAO_ID,
                COMPANY_CD = entity.COMPANY_CD,
                PBAN = entity.PBAN,
                MSO = entity.MSO,
                TEN = entity.TEN,
                LOAI = entity.LOAI,
                MCQT = entity.MCQT,
                TCQT = entity.TCQT,
                SO = entity.SO,
                NTBCCQT = entity.NTBCCQT,
                MST = entity.MST,
                TNNT = entity.TNNT,
                DDANH = entity.DDANH,
                NTBAO = entity.NTBAO,
                XML = entity.XML,
                IS_SIGNED = entity.IS_SIGNED,
                IS_MAIL = entity.IS_MAIL,
                MGDDTU = entity.MGDDTU,
                MTDIEP = entity.MTDIEP,
                ERROR_MESSAGE = entity.ERROR_MESSAGE,
                ISDEL = entity.ISDEL,
                DETAILS = entity.DETAILS.Select(ToDetailDto).ToList()
            };
        }

        public static EInvoiceErrorNoticeDetailDto ToDetailDto(EInvoiceErrorNoticeDetail entity)
        {
            return new EInvoiceErrorNoticeDetailDto
            {
                DETAIL_ID = entity.DETAIL_ID,
                TBAO_ID = entity.TBAO_ID,
                COMPANY_CD = entity.COMPANY_CD,
                STT = entity.STT,
                MCCQT = entity.MCCQT,
                KHMSHDON = entity.KHMSHDON,
                KHHDON = entity.KHHDON,
                SHDON = entity.SHDON,
                NGAY = entity.NGAY,
                LADHDDT = entity.LADHDDT,
                LDO = entity.LDO,
                ISDEL = entity.ISDEL
            };
        }

        public static EInvoiceErrorNoticeInfo ToEntity(EInvoiceErrorNoticeSaveRequest request, string companyCd)
        {
            return new EInvoiceErrorNoticeInfo
            {
                TBAO_ID = request.TBAO_ID,
                COMPANY_CD = companyCd,
                MSO = request.MSO,
                TEN = request.TEN,
                LOAI = request.LOAI,
                MCQT = request.MCQT,
                TCQT = request.TCQT,
                SO = request.SO,
                NTBCCQT = request.NTBCCQT,
                MST = request.MST,
                TNNT = request.TNNT,
                DDANH = request.DDANH,
                NTBAO = request.NTBAO,
                XML = request.XML,
                IS_SIGNED = request.IS_SIGNED,
                ERROR_MESSAGE = request.ERROR_MESSAGE,
                ISDEL = request.ISDEL,
                DETAILS = request.DETAILS.Select(x => ToDetailEntity(x, companyCd, request.TBAO_ID)).ToList()
            };
        }

        public static EInvoiceErrorNoticeDetail ToDetailEntity(EInvoiceErrorNoticeDetailDto request, string companyCd, long tbaoId)
        {
            return new EInvoiceErrorNoticeDetail
            {
                DETAIL_ID = request.DETAIL_ID,
                TBAO_ID = tbaoId,
                COMPANY_CD = companyCd,
                STT = request.STT,
                MCCQT = request.MCCQT,
                KHMSHDON = request.KHMSHDON,
                KHHDON = request.KHHDON,
                SHDON = request.SHDON,
                NGAY = request.NGAY,
                LADHDDT = request.LADHDDT,
                LDO = request.LDO,
                ISDEL = request.ISDEL
            };
        }
    }
}
