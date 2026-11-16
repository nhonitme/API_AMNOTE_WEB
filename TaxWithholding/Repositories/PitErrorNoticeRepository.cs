using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using System.Globalization;
namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitErrorNoticeRepository(DapperExecutor db) : IPitErrorNoticeRepository
{
    public Task<(IReadOnlyList<PitErrorNotice> First, IReadOnlyList<PitCountRow> Second)> SearchAsync(object parameters, string? dbName)
        => db.QueryMultipleAsync<PitErrorNotice, PitCountRow>(Net_DB.Net_DB_Company,
            "CALL getPitTbao(@company,@id,@from,@to,@keyword,@signed,@cqtStatus,@offset,@pageSize)", parameters, sDBName: dbName);

    public async Task<long> SaveAsync(DapperSession s, string company, string user, long id, int version, PitErrorNoticeDto d)
    {
        var result = await s.QuerySingleAsync<long>("CALL setPitTbao(@company,@id,@version,@noticeKind,@noticeNo,@noticeDate,@officeCd,@office,@name,@tax,@budget,@location,@date,@user)",
            new
            {
                company,
                id,
                version,
                noticeKind = d.LOAI,
                noticeNo = d.SO,
                noticeDate = d.NTBCCQT,
                officeCd = d.MCQT,
                office = d.TCQT,
                name = d.TNNT,
                tax = d.MST,
                budget = d.MDVQHNSACH,
                location = d.DDANH,
                date = d.NTBAO,
                user,
            });
        await s.ExecuteAsync("CALL delPitTbaoCtu(@company,@id)", new { company, id = result });
        for (var i = 0; i < d.Items.Count; i++)
        {
            var x = d.Items[i]; await s.ExecuteAsync("CALL setPitTbaoCtu(@company,@id,@refId,@stt,@form,@series,@number,@date,@type,@reason,@user)",
            new
            {
                company,
                id = result,
                refId = x.REF_CTU_ID,
                stt = i + 1,
                form = x.KHMSCTU,
                series = x.KHCTU,
                number = x.SCTU,
                date = x.NLAP,
                type = x.LCTDT,
                reason = x.LDO,
                user,
            });
        }
        return result;
    }
}
