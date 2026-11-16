using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using System.Globalization;
namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitCertificateRepository(DapperExecutor db) : IPitCertificateRepository
{
    public Task<(IReadOnlyList<PitCertificate> First, IReadOnlyList<PitCountRow> Second)> SearchAsync(object parameters, string? dbName)
        => db.QueryMultipleAsync<PitCertificate, PitCountRow>(Net_DB.Net_DB_Company,
            "CALL getPitCtu(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)", parameters, sDBName: dbName);

    public Task<long> SaveAsync(DapperSession s, string company, string user, long id, int version, long? xslId, PitCertificateDto d)
    {
        return s.QuerySingleAsync<long>("CALL setPitCtu(@company,@id,@version,@xsl,@series,@date,@relatedKind,@relatedType,@relatedForm,@relatedSeries,@relatedNo,@relatedDate,@relatedNote,@payerName,@payerTax,@payerAddress,@payerPhone,@personName,@personTax,@personAddress,@nationality,@resident,@identity,@personPhone,@personEmail,@personNote,@incomeKind,@fromMonth,@toMonth,@year,@insurance,@charity,@taxable,@assessable,@withheld,@user)",
            new
            {
                company,
                id,
                version,
                xsl = xslId,
                series = d.KHCTU,
                date = d.NLAP,
                relatedKind = d.TCCTU,
                relatedType = d.LHCTLQUAN,
                relatedForm = d.KHMSCTCLQUAN,
                relatedSeries = d.KHCTCLQUAN,
                relatedNo = d.SCTCLQUAN,
                relatedDate = d.NLCTCLQUAN,
                relatedNote = d.GCHU,
                payerName = d.TCTTNHAP_TEN,
                payerTax = d.TCTTNHAP_MST,
                payerAddress = d.TCTTNHAP_DCHI,
                payerPhone = d.TCTTNHAP_SDTHOAI,
                personName = d.NNT_TEN,
                personTax = d.NNT_MST,
                personAddress = d.NNT_DCHI,
                nationality = d.NNT_QTICH,
                resident = d.NNT_CNCTRU,
                identity = d.NNT_CCCDAN,
                personPhone = d.NNT_SDTHOAI,
                personEmail = d.NNT_DCTDTU,
                personNote = d.NNT_GCHU,
                incomeKind = d.KTNHAP,
                fromMonth = d.TTHANG,
                toMonth = d.DTHANG,
                year = d.NAM,
                insurance = d.BHIEM,
                charity = d.TTHIEN,
                taxable = d.TTNCTHUE,
                assessable = d.TTNTTHUE,
                withheld = d.STHUE,
                user,
            });
    }
}
